using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Services;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Audit;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Profiles;
using OpenTheWindows.Core.Releases;
using OpenTheWindows.Core.Updates;
using OpenTheWindows.Windows;
using OpenTheWindows.Windows.Readers;
using OpenTheWindows.Windows.Security;

namespace OpenTheWindows.App;

/// <summary>
/// Composition root of the GUI. Builds the service container, resolves the
/// main window, and disposes the container on exit.
/// </summary>
internal sealed partial class DesktopApp : Application, IDisposable
{
    private ServiceProvider? _services;

    /// <summary>
    /// The machine trust store of profile-signing keys and the <c>RequireSignedProfiles</c>
    /// policy, which together decide whether an imported profile file may be applied.
    /// </summary>
    private static void RegisterProfileTrust(IServiceCollection services)
    {
        services.AddSingleton<ITrustedKeySource, WindowsTrustedKeySource>();
        services.AddSingleton(_ => ProfilePolicy.Read(new WindowsRegistryReader()));
    }

    /// <summary>Registers every service the GUI needs. Internal so tests can validate the graph.</summary>
    public static IServiceCollection ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // OS abstractions + diagnostics (the only OS-touching registrations).
        services.AddSingleton<IOperatingSystemInfo, WindowsOperatingSystemInfo>();
        services.AddSingleton<IElevationContext, WindowsElevationContext>();
        services.AddSingleton<DoctorService>();

        // Catalogue + machine facts, loaded once and shared by the category pages.
        services.AddSingleton<TweakCatalog>(_ => CatalogLoader.LoadBuiltIn().Catalog
            ?? throw new InvalidOperationException("The built-in catalogue failed to load."));
        services.AddSingleton<OperatingSystemFacts>(sp => sp.GetRequiredService<DoctorService>().Run().OperatingSystem);

        // Apply pipeline (the engine is built lazily by the coordinator, off the DI-validation path).
        services.AddSingleton<IExplorerRestarter, WindowsExplorerRestarter>();
        services.AddSingleton<IInteractiveUserResolver, WindowsInteractiveUserResolver>();
        services.AddSingleton<Func<ApplyEngine>>(_ => static () => WindowsApplyEngineFactory.Create());
        services.AddSingleton<Func<ScanEngine>>(_ => static () => WindowsScanEngineFactory.Create());
        services.AddSingleton<IApplyCoordinator, ApplyCoordinator>();
        RegisterActivity(services);
        services.AddTransient<ApplyFlowViewModel>();
        services.AddSingleton<Func<ApplyFlowViewModel>>(sp => sp.GetRequiredService<ApplyFlowViewModel>);
        services.AddSingleton<IApplyFlowLauncher, ApplyFlowLauncher>();
        services.AddSingleton<IFileDialogService, FileDialogService>();
        services.AddSingleton(_ => GitHubAppReleaseChecker.CreateClient());
        services.AddSingleton<IAppReleaseChecker>(sp => new GitHubAppReleaseChecker(sp.GetRequiredService<HttpClient>()));
        RegisterUpdates(services);
        RegisterProfileTrust(services);
        RegisterAudit(services);

        // Shell services.
        services.AddSingleton<IGuiSettingsStore, FileGuiSettingsStore>();
        services.AddSingleton<AppSettings>(sp => new AppSettings(sp.GetRequiredService<IGuiSettingsStore>()));
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IDispatcherService>(_ => new WpfDispatcherService(Dispatcher.CurrentDispatcher));
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IReadOnlyList<NavigationItem>>(ShellNavigation.Pages);

        RegisterPages(services);

        // Shell.
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        return services;
    }

    // Page view models, keyed by page so the navigation service resolves them.
    private static void RegisterPages(IServiceCollection services)
    {
        services.AddSingleton<DoctorViewModel>();
        services.AddKeyedSingleton<IPageViewModel, DashboardViewModel>(PageKeys.Dashboard);
        services.AddKeyedSingleton<IPageViewModel, UpdatesViewModel>(PageKeys.Updates);
        foreach (Category category in new[] { Category.Privacy, Category.Security, Category.Performance, Category.Debloat, Category.Shell })
        {
            services.AddKeyedSingleton<IPageViewModel>(
                CategoryPages.KeyFor(category),
                (sp, _) => ActivatorUtilities.CreateInstance<CategoryPageViewModel>(sp, category));
        }

        services.AddKeyedSingleton<IPageViewModel, StartupViewModel>(PageKeys.Startup);
        services.AddKeyedSingleton<IPageViewModel, AccessViewModel>(PageKeys.Access);
        services.AddKeyedSingleton<IPageViewModel, ProfilesViewModel>(PageKeys.Profiles);
        services.AddKeyedSingleton<IPageViewModel, HistoryViewModel>(PageKeys.History);
        services.AddKeyedSingleton<IPageViewModel, SettingsViewModel>(PageKeys.Settings);
        services.AddKeyedSingleton<IPageViewModel, AboutViewModel>(PageKeys.About);
    }

    // Startup apps, the app-access view and the persistence watch, each built lazily by the
    // coordinator off the DI-validation path.
    private static void RegisterActivity(IServiceCollection services)
    {
        services.AddSingleton<IActivityCoordinator>(_ => new ActivityCoordinator(
            WindowsActivityFactory.CreateStartupApps, WindowsActivityFactory.CreateCapabilityAccess, WindowsActivityFactory.CreatePersistenceWatch));
    }

    // Windows Update control: pause/resume, per-update hide, the feature-update hold and the
    // supervised repair, each built lazily by the coordinator off the DI-validation path.
    private static void RegisterUpdates(IServiceCollection services)
    {
        services.AddSingleton<Func<UpdateControlService>>(_ => static () => WindowsUpdateControlFactory.Create());
        services.AddSingleton<Func<IUpdateHideService>>(_ => static () => WindowsUpdateHideFactory.Create());
        services.AddSingleton<Func<UpdateTargetService>>(_ => static () => WindowsUpdateControlFactory.CreateTarget());
        services.AddSingleton<Func<IUpdateRepair>>(_ => static () => WindowsUpdateControlFactory.CreateRepair());
        services.AddSingleton<IUpdateCoordinator, UpdateCoordinator>();
    }

    // Audit (read-only): the built-in baselines, the health probe, and the audit
    // engine built lazily by the coordinator off the DI-validation path.
    private static void RegisterAudit(IServiceCollection services)
    {
        services.AddSingleton<IMachineHealthProbe, WindowsMachineHealthProbe>();
        services.AddSingleton<IReadOnlyList<Baseline>>(_ => BaselineLoader.LoadBuiltIn().Baselines);
        services.AddSingleton<Func<AuditEngine>>(sp => () => new AuditEngine(
            WindowsScanEngineFactory.Create(), sp.GetRequiredService<IMachineHealthProbe>(), sp.GetRequiredService<TweakCatalog>()));
        services.AddSingleton<IAuditCoordinator, AuditCoordinator>();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _services?.Dispose();
        _services = null;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // An unexpected fault is logged and shown instead of closing the window under the user.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        _services?.Dispose();
        _services = ConfigureServices(new ServiceCollection()).BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        AppSettings settings = _services.GetRequiredService<AppSettings>();
        _services.GetRequiredService<IThemeService>().Apply(settings.Theme);

        MainWindow window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        bool logged = GuiErrorLog.Append(GuiErrorLog.DefaultPath, TimeProvider.System.GetUtcNow(), e.Exception);
        string details = logged
            ? string.Create(CultureInfo.CurrentCulture, $"Details were saved to {GuiErrorLog.DefaultPath}.")
            : "The details could not be saved.";
        MessageBox.Show(
            MainWindow,
            string.Create(CultureInfo.CurrentCulture,
                $"Something went wrong: {e.Exception.Message}{Environment.NewLine}{Environment.NewLine}Every change this app makes is journaled, and a run that did not finish is rolled back the next time you apply or revert. {details}"),
            "Open the Windows",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        GuiErrorLog.Append(GuiErrorLog.DefaultPath, TimeProvider.System.GetUtcNow(), e.Exception);
        e.SetObserved();
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            GuiErrorLog.Append(GuiErrorLog.DefaultPath, TimeProvider.System.GetUtcNow(), exception);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Dispose();
        base.OnExit(e);
    }
}
