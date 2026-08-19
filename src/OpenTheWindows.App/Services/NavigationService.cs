using Microsoft.Extensions.DependencyInjection;
using OpenTheWindows.App.ViewModels;

namespace OpenTheWindows.App.Services;

/// <summary>
/// <see cref="INavigationService"/> that resolves page view models from the DI
/// container by their <see cref="Navigation.PageKeys"/> key (registered as keyed
/// <see cref="IPageViewModel"/> singletons, so page state survives navigation).
/// </summary>
internal sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _provider;
    private readonly Stack<string> _back = new();

    /// <summary>Creates the service over the application's <paramref name="provider"/>.</summary>
    public NavigationService(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
    }

    /// <inheritdoc />
    public object? CurrentPage { get; private set; }

    /// <inheritdoc />
    public string? CurrentPageKey { get; private set; }

    /// <inheritdoc />
    public bool CanGoBack => _back.Count > 0;

    /// <inheritdoc />
    public event EventHandler? CurrentPageChanged;

    /// <inheritdoc />
    public void NavigateTo(string pageKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(pageKey);
        if (string.Equals(pageKey, CurrentPageKey, StringComparison.Ordinal))
        {
            return;
        }

        if (CurrentPageKey is { } leaving)
        {
            _back.Push(leaving);
        }

        Show(pageKey);
    }

    /// <inheritdoc />
    public void GoBack()
    {
        if (_back.Count == 0)
        {
            return;
        }

        Show(_back.Pop());
    }

    private void Show(string pageKey)
    {
        IPageViewModel page = _provider.GetRequiredKeyedService<IPageViewModel>(pageKey);
        CurrentPage = page;
        CurrentPageKey = pageKey;
        (page as IActivatable)?.OnActivated();
        CurrentPageChanged?.Invoke(this, EventArgs.Empty);
    }
}
