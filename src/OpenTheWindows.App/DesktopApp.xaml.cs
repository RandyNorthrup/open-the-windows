using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Services;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Windows;
using OpenTheWindows.Windows.Readers;

namespace OpenTheWindows.App;

/// <summary>
/// Composition root of the GUI. Builds the service container, resolves the
/// main window, and disposes the container on exit.
/// </summary>
internal sealed partial class DesktopApp : Application, IDisposable
{
    private ServiceProvider? _services;

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
        services.AddSingleton<IApplyCoordinator, ApplyCoordinator>();
        services.AddTransient<ApplyFlowViewModel>();
        services.AddSingleton<Func<ApplyFlowViewModel>>(sp => sp.GetRequiredService<ApplyFlowViewModel>);
        services.AddSingleton<IApplyFlowLauncher, ApplyFlowLauncher>();
        services.AddSingleton<IFileDialogService, FileDialogService>();

        // Shell services.
        services.AddSingleton<IDispatcherService>(_ => new WpfDispatcherService(Dispatcher.CurrentDispatcher));
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IReadOnlyList<NavigationItem>>(ShellNavigation.Pages);

        // Page view models (keyed by page so the navigation service resolves them).
        services.AddSingleton<DoctorViewModel>();
        services.AddKeyedSingleton<IPageViewModel, DashboardViewModel>(PageKeys.Dashboard);
        foreach (Category category in new[] { Category.Privacy, Category.Security, Category.Performance, Category.Debloat, Category.Shell })
        {
            services.AddKeyedSingleton<IPageViewModel>(
                CategoryPages.KeyFor(category),
                (sp, _) => ActivatorUtilities.CreateInstance<CategoryPageViewModel>(sp, category));
        }

        services.AddKeyedSingleton<IPageViewModel, ProfilesViewModel>(PageKeys.Profiles);
        services.AddKeyedSingleton<IPageViewModel, HistoryViewModel>(PageKeys.History);
        services.AddKeyedSingleton<IPageViewModel, AboutViewModel>(PageKeys.About);

        // Shell.
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        return services;
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

        _services?.Dispose();
        _services = ConfigureServices(new ServiceCollection()).BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        MainWindow window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Dispose();
        base.OnExit(e);
    }
}
