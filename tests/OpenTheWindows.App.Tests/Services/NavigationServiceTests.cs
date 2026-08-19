using Microsoft.Extensions.DependencyInjection;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Services;
using OpenTheWindows.App.ViewModels;

namespace OpenTheWindows.App.Tests.Services;

/// <summary>Resolution, current-page tracking and change notification of the real navigation service.</summary>
public sealed class NavigationServiceTests
{
    private sealed class StubPage(string key) : IPageViewModel
    {
        public string Key { get; } = key;
        public string Title => Key;
    }

    private static (NavigationService Navigation, ServiceProvider Provider) Build()
    {
        ServiceProvider provider = new ServiceCollection()
            .AddKeyedSingleton<IPageViewModel>(PageKeys.Dashboard, (_, _) => new StubPage(PageKeys.Dashboard))
            .AddKeyedSingleton<IPageViewModel>(PageKeys.About, (_, _) => new StubPage(PageKeys.About))
            .BuildServiceProvider();
        return (new NavigationService(provider), provider);
    }

    [Fact]
    public void NavigateTo_resolves_the_keyed_page_and_raises_the_change()
    {
        (NavigationService navigation, ServiceProvider provider) = Build();
        using (provider)
        {
            int raised = 0;
            navigation.CurrentPageChanged += (_, _) => raised++;

            navigation.NavigateTo(PageKeys.About);

            Assert.Equal(PageKeys.About, navigation.CurrentPageKey);
            StubPage page = Assert.IsType<StubPage>(navigation.CurrentPage);
            Assert.Equal(PageKeys.About, page.Key);
            Assert.Equal(1, raised);
        }
    }

    [Fact]
    public void Navigating_to_the_current_page_is_a_no_op()
    {
        (NavigationService navigation, ServiceProvider provider) = Build();
        using (provider)
        {
            navigation.NavigateTo(PageKeys.Dashboard);
            int raised = 0;
            navigation.CurrentPageChanged += (_, _) => raised++;

            navigation.NavigateTo(PageKeys.Dashboard);

            Assert.Equal(0, raised);
        }
    }

    [Fact]
    public void An_unregistered_page_key_throws()
    {
        (NavigationService navigation, ServiceProvider provider) = Build();
        using (provider)
        {
            Assert.Throws<InvalidOperationException>(() => navigation.NavigateTo("NoSuchPage"));
        }
    }
}
