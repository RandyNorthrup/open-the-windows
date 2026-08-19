using OpenTheWindows.App.ViewModels;

namespace OpenTheWindows.App.Services;

/// <summary>Applies the chosen theme to the running application. A UI seam; a fake records the choice in tests.</summary>
internal interface IThemeService
{
    /// <summary>Applies <paramref name="theme"/> to the application immediately.</summary>
    void Apply(AppTheme theme);
}
