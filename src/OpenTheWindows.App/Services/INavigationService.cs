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

    /// <summary>Raised after <see cref="CurrentPage"/> changes.</summary>
    event EventHandler? CurrentPageChanged;

    /// <summary>
    /// Switches the shell to the page registered under <paramref name="pageKey"/>.
    /// The page view model is resolved from the container (singleton, so page
    /// state such as a level dial survives navigation).
    /// </summary>
    void NavigateTo(string pageKey);
}
