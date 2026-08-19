namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// A page that wants to refresh when it becomes the visible page. The
/// <see cref="Services.INavigationService"/> calls <see cref="OnActivated"/>
/// after navigating to the page, so live data (history, drift) is not read
/// until the page is actually shown.
/// </summary>
internal interface IActivatable
{
    /// <summary>Called each time this page becomes the current page.</summary>
    void OnActivated();
}
