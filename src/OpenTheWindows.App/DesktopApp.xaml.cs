using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Services;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Windows;

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

        // Shell services.
        services.AddSingleton<IDispatcherService>(_ => new WpfDispatcherService(Dispatcher.CurrentDispatcher));
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IReadOnlyList<NavigationItem>>(ShellNavigation.Pages);

        // Page view models (keyed by page so the navigation service resolves them).
        services.AddSingleton<DoctorViewModel>();
        services.AddKeyedSingleton<IPageViewModel, DashboardViewModel>(PageKeys.Dashboard);
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
