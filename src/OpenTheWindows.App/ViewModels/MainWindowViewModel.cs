using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Services;
using OpenTheWindows.Core;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// Shell-level state: the product identity, the navigation list, the selected
/// item and the currently hosted page. Navigation is delegated to the
/// <see cref="INavigationService"/> so pages can navigate without a shell
/// back-reference.
/// </summary>
internal sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private NavigationItem? _selectedItem;

    /// <summary>Builds the shell over the navigation service and the page list.</summary>
    public MainWindowViewModel(INavigationService navigation, IReadOnlyList<NavigationItem> pages)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(pages);
        _navigation = navigation;
        _navigation.CurrentPageChanged += OnCurrentPageChanged;

        NavigationItems = new ObservableCollection<NavigationItem>(pages);
        SelectedItem = NavigationItems.Count > 0 ? NavigationItems[0] : null;
    }

    /// <summary>The window title bar text.</summary>
    public static string WindowTitle => $"{AppInfo.Title} {AppInfo.Version}";

    /// <summary>The product name shown in the shell header.</summary>
    public static string Title => AppInfo.Title;

    /// <summary>The informational version shown in the shell footer.</summary>
    public static string Version => AppInfo.Version;

    /// <summary>The pages offered in the left navigation list.</summary>
    public ObservableCollection<NavigationItem> NavigationItems { get; }

    /// <summary>The page view model currently hosted in the content area.</summary>
    public object? CurrentPage => _navigation.CurrentPage;

    partial void OnSelectedItemChanged(NavigationItem? value)
    {
        if (value is not null)
        {
            _navigation.NavigateTo(value.Key);
        }
    }

    private void OnCurrentPageChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(CurrentPage));
}
