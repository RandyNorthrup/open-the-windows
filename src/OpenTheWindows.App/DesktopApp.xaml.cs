using System.Windows;
using Microsoft.Extensions.DependencyInjection;
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

        services.AddSingleton<IOperatingSystemInfo, WindowsOperatingSystemInfo>();
        services.AddSingleton<IElevationContext, WindowsElevationContext>();
        services.AddSingleton<DoctorService>();
        services.AddSingleton<DoctorViewModel>();
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
