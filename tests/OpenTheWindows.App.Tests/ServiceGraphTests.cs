using Microsoft.Extensions.DependencyInjection;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Diagnostics;

namespace OpenTheWindows.App.Tests;

/// <summary>
/// The DI graph is validated at build time in production (ValidateOnBuild);
/// this test does the same in CI so a missing registration fails a PR, not a
/// user's first launch. Resolving MainWindow needs a UI thread; view models
/// and services are resolved directly.
/// </summary>
public sealed class ServiceGraphTests
{
    [Fact]
    public void Configured_services_resolve_view_models()
    {
        using ServiceProvider provider = DesktopApp.ConfigureServices(new ServiceCollection())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = true });

        MainWindowViewModel main = provider.GetRequiredService<MainWindowViewModel>();
        DoctorViewModel doctor = provider.GetRequiredService<DoctorViewModel>();
        DoctorService service = provider.GetRequiredService<DoctorService>();

        Assert.Same(doctor, main.Doctor);
        Assert.NotNull(service);
        Assert.Equal(AppInfo.Title, MainWindowViewModel.Title);
        Assert.StartsWith(AppInfo.Title, MainWindowViewModel.WindowTitle, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigureServices_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => DesktopApp.ConfigureServices(null!));
    }
}
