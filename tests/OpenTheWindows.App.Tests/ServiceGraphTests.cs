using Microsoft.Extensions.DependencyInjection;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core;

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
    public void Configured_services_resolve_the_shell_and_navigate_to_the_dashboard()
    {
        using ServiceProvider provider = DesktopApp.ConfigureServices(new ServiceCollection())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = true });

        MainWindowViewModel shell = provider.GetRequiredService<MainWindowViewModel>();

        Assert.NotEmpty(shell.NavigationItems);
        Assert.Equal(ShellNavigation.Pages.Count, shell.NavigationItems.Count);
        Assert.IsType<DashboardViewModel>(shell.CurrentPage);
        Assert.Equal(AppInfo.Title, MainWindowViewModel.Title);
        Assert.StartsWith(AppInfo.Title, MainWindowViewModel.WindowTitle, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_navigation_page_resolves_by_key()
    {
        using ServiceProvider provider = DesktopApp.ConfigureServices(new ServiceCollection())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false, ValidateScopes = true });

        foreach (NavigationItem page in ShellNavigation.Pages)
        {
            IPageViewModel resolved = provider.GetRequiredKeyedService<IPageViewModel>(page.Key);
            Assert.Equal(page.Key, resolved.Key);
        }
    }

    [Fact]
    public void ConfigureServices_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => DesktopApp.ConfigureServices(null!));
    }
}
