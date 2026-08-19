using System.Windows;
using OpenTheWindows.App.ViewModels;

namespace OpenTheWindows.App.Services;

/// <summary>
/// <see cref="IThemeService"/> that sets the application's Fluent
/// <see cref="ThemeMode"/>. The production implementation; tests use a fake.
/// </summary>
internal sealed class ThemeService : IThemeService
{
    /// <inheritdoc />
    public void Apply(AppTheme theme)
    {
        if (Application.Current is { } app)
        {
            app.ThemeMode = theme switch
            {
                AppTheme.Light => ThemeMode.Light,
                AppTheme.Dark => ThemeMode.Dark,
                _ => ThemeMode.System,
            };
        }
    }
}
