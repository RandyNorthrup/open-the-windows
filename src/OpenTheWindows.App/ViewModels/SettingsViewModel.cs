using CommunityToolkit.Mvvm.ComponentModel;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Resources;
using OpenTheWindows.App.Services;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// The Settings page. Edits the shared <see cref="AppSettings"/> (restore point,
/// automatic Explorer restart, draft entries, enterprise mode) and applies the
/// theme immediately. Language is en-US only in M6 (the resx infrastructure is
/// in place for future translations).
/// </summary>
internal sealed class SettingsViewModel : ObservableObject, IPageViewModel
{
    private readonly IThemeService _themeService;

    /// <summary>Creates the page over the shared settings and the theme service.</summary>
    public SettingsViewModel(AppSettings settings, IThemeService themeService)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(themeService);
        Settings = settings;
        _themeService = themeService;
    }

    /// <inheritdoc />
    public string Key => PageKeys.Settings;

    /// <inheritdoc />
    public string Title => Strings.Nav_Settings;

    /// <summary>The shared settings the toggles bind to.</summary>
    public AppSettings Settings { get; }

    /// <summary>The available themes.</summary>
    public IReadOnlyList<AppTheme> Themes { get; } = [AppTheme.System, AppTheme.Light, AppTheme.Dark];

    /// <summary>The available languages (en-US only in M6).</summary>
    public IReadOnlyList<string> Languages { get; } = ["en-US"];

    /// <summary>The selected language (read-only in M6).</summary>
    public string SelectedLanguage => Languages[0];

    /// <summary>The chosen theme; setting it applies the theme immediately.</summary>
    public AppTheme SelectedTheme
    {
        get => Settings.Theme;
        set
        {
            if (Settings.Theme == value)
            {
                return;
            }

            Settings.Theme = value;
            _themeService.Apply(value);
            OnPropertyChanged();
        }
    }
}
