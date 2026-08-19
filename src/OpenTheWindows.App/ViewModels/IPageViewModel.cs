namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// A top-level page hosted by the shell. Implemented by every page view model
/// so the shell can list them and the <see cref="Services.INavigationService"/>
/// can resolve one by <see cref="Key"/>.
/// </summary>
internal interface IPageViewModel
{
    /// <summary>The page's stable <see cref="Navigation.PageKeys"/> identifier.</summary>
    string Key { get; }

    /// <summary>The page's display title, shown in the navigation list and page header.</summary>
    string Title { get; }
}
