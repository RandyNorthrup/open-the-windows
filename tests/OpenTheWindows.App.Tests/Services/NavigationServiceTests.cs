using Microsoft.Extensions.DependencyInjection;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Services;
using OpenTheWindows.App.ViewModels;

namespace OpenTheWindows.App.Tests.Services;

/// <summary>Resolution, current-page tracking and change notification of the real navigation service.</summary>
public sealed class NavigationServiceTests
{
    private sealed class StubPage(string key) : IPageViewModel, IActivatable
    {
        public string Key { get; } = key;
        public string Title => Key;
        public int ActivatedCount { get; private set; }
        public void OnActivated() => ActivatedCount++;
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
    public void NavigateTo_activates_the_page()
    {
        (NavigationService navigation, ServiceProvider provider) = Build();
        using (provider)
        {
            navigation.NavigateTo(PageKeys.About);

            StubPage page = Assert.IsType<StubPage>(navigation.CurrentPage);
            Assert.Equal(1, page.ActivatedCount);
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

    [Fact]
    public void Back_is_unavailable_until_a_second_page_is_shown()
    {
        (NavigationService navigation, ServiceProvider provider) = Build();
        using (provider)
        {
            Assert.False(navigation.CanGoBack);

            navigation.NavigateTo(PageKeys.Dashboard);
            Assert.False(navigation.CanGoBack);

            navigation.NavigateTo(PageKeys.About);
            Assert.True(navigation.CanGoBack);
        }
    }

    [Fact]
    public void GoBack_returns_to_the_previous_page_and_empties_the_stack()
    {
        (NavigationService navigation, ServiceProvider provider) = Build();
        using (provider)
        {
            navigation.NavigateTo(PageKeys.Dashboard);
            navigation.NavigateTo(PageKeys.About);

            navigation.GoBack();

            Assert.Equal(PageKeys.Dashboard, navigation.CurrentPageKey);
            Assert.False(navigation.CanGoBack);
        }
    }

    [Fact]
    public void GoBack_does_nothing_with_an_empty_stack()
    {
        (NavigationService navigation, ServiceProvider provider) = Build();
        using (provider)
        {
            navigation.NavigateTo(PageKeys.Dashboard);
            int raised = 0;
            navigation.CurrentPageChanged += (_, _) => raised++;

            navigation.GoBack();

            Assert.Equal(PageKeys.Dashboard, navigation.CurrentPageKey);
            Assert.Equal(0, raised);
        }
    }
}
