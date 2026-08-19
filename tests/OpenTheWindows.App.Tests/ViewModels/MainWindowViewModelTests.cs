using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Tests.Fakes;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core;

namespace OpenTheWindows.App.Tests.ViewModels;

/// <summary>Shell navigation behaviour, driven with a fake navigation service (no UI thread).</summary>
public sealed class MainWindowViewModelTests
{
    private static readonly IReadOnlyList<NavigationItem> Pages =
    [
        new(PageKeys.Dashboard, "Dashboard"),
        new(PageKeys.About, "About"),
    ];

    [Fact]
    public void Selects_and_navigates_to_the_first_page_on_construction()
    {
        FakeNavigationService navigation = new();

        MainWindowViewModel shell = new(navigation, Pages);

        Assert.Equal(Pages[0], shell.SelectedItem);
        Assert.Equal([PageKeys.Dashboard], navigation.Navigations);
    }

    [Fact]
    public void Changing_the_selection_navigates_to_that_page()
    {
        FakeNavigationService navigation = new();
        MainWindowViewModel shell = new(navigation, Pages);
        Assert.Equal(Pages[0], shell.SelectedItem);

        shell.SelectedItem = Pages[1];

        Assert.Equal([PageKeys.Dashboard, PageKeys.About], navigation.Navigations);
    }

    [Fact]
    public void CurrentPage_changes_are_raised_as_property_changes()
    {
        FakeNavigationService navigation = new();
        MainWindowViewModel shell = new(navigation, Pages);
        List<string?> changed = [];
        shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        navigation.SetCurrentPage(new object());

        Assert.Contains(nameof(MainWindowViewModel.CurrentPage), changed, StringComparer.Ordinal);
    }

    [Fact]
    public void Exposes_product_identity()
    {
        Assert.Equal(AppInfo.Title, MainWindowViewModel.Title);
        Assert.StartsWith(AppInfo.Title, MainWindowViewModel.WindowTitle, StringComparison.Ordinal);
        Assert.Equal(AppInfo.Version, MainWindowViewModel.Version);
    }

    [Fact]
    public void Rejects_null_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new MainWindowViewModel(null!, Pages));
        Assert.Throws<ArgumentNullException>(() => new MainWindowViewModel(new FakeNavigationService(), null!));
    }
}
