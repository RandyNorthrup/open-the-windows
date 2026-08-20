using OpenTheWindows.App.Services;

namespace OpenTheWindows.App.Tests.Fakes;

/// <summary>
/// In-memory <see cref="INavigationService"/> for view-model tests: records every
/// navigation and lets a test drive <see cref="CurrentPage"/> to verify that a
/// view model reacts to <see cref="CurrentPageChanged"/>.
/// </summary>
internal sealed class FakeNavigationService : INavigationService
{
    /// <summary>The page keys passed to <see cref="NavigateTo"/>, in order.</summary>
    public List<string> Navigations { get; } = [];

    /// <inheritdoc />
    public object? CurrentPage { get; private set; }

    /// <inheritdoc />
    public string? CurrentPageKey { get; private set; }

    /// <inheritdoc />
    public event EventHandler? CurrentPageChanged;

    /// <inheritdoc />
    public void NavigateTo(string pageKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(pageKey);
        Navigations.Add(pageKey);
        // Mirror the real service: set the current page/key, then raise the change.
        CurrentPage = pageKey;
        CurrentPageKey = pageKey;
        CurrentPageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Sets the current page and raises <see cref="CurrentPageChanged"/>, as the real service does after resolving a page.</summary>
    public void SetCurrentPage(object page)
    {
        CurrentPage = page;
        CurrentPageChanged?.Invoke(this, EventArgs.Empty);
    }
}
