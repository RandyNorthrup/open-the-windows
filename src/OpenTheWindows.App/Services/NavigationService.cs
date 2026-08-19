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
    public event EventHandler? CurrentPageChanged;

    /// <inheritdoc />
    public void NavigateTo(string pageKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(pageKey);
        if (string.Equals(pageKey, CurrentPageKey, StringComparison.Ordinal))
        {
            return;
        }

        CurrentPage = _provider.GetRequiredKeyedService<IPageViewModel>(pageKey);
        CurrentPageKey = pageKey;
        CurrentPageChanged?.Invoke(this, EventArgs.Empty);
    }
}
