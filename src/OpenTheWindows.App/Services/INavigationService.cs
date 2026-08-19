namespace OpenTheWindows.App.Services;

/// <summary>
/// Owns which page view model the shell is showing. View models navigate
/// through this service instead of referencing the shell, so a quick action on
/// one page can open another without a back-reference.
/// </summary>
internal interface INavigationService
{
    /// <summary>The page view model currently hosted by the shell, or <see langword="null"/> before first navigation.</summary>
    object? CurrentPage { get; }

    /// <summary>The key of the current page, or <see langword="null"/> before first navigation.</summary>
    string? CurrentPageKey { get; }

    /// <summary>Whether there is a previous page to return to (the back stack is not empty).</summary>
    bool CanGoBack { get; }

    /// <summary>Raised after <see cref="CurrentPage"/> changes.</summary>
    event EventHandler? CurrentPageChanged;

    /// <summary>
    /// Switches the shell to the page registered under <paramref name="pageKey"/>.
    /// The page view model is resolved from the container (singleton, so page
    /// state such as a level dial survives navigation). The page being left is
    /// pushed onto the back stack so <see cref="GoBack"/> can return to it.
    /// </summary>
    void NavigateTo(string pageKey);

    /// <summary>
    /// Returns to the previous page on the back stack. Does nothing when
    /// <see cref="CanGoBack"/> is <see langword="false"/>. Going back does not
    /// itself push onto the stack, so repeated back navigation walks the history.
    /// </summary>
    void GoBack();
}
