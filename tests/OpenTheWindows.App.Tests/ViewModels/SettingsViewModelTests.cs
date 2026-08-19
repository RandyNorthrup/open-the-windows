using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Tests.Fakes;
using OpenTheWindows.App.ViewModels;

namespace OpenTheWindows.App.Tests.ViewModels;

/// <summary>The Settings page: edits the shared settings and applies the theme live.</summary>
public sealed class SettingsViewModelTests
{
    private readonly AppSettings _settings = new();
    private readonly FakeThemeService _theme = new();

    private SettingsViewModel Build() => new(_settings, _theme);

    [Fact]
    public void Is_the_settings_page_and_edits_the_shared_settings()
    {
        SettingsViewModel page = Build();

        Assert.Equal(PageKeys.Settings, page.Key);
        Assert.Same(_settings, page.Settings);
        Assert.Equal(3, page.Themes.Count);
        Assert.Equal("en-US", page.SelectedLanguage);
    }

    [Fact]
    public void Choosing_a_theme_applies_it_and_records_the_choice()
    {
        SettingsViewModel page = Build();

        page.SelectedTheme = AppTheme.Dark;

        Assert.Equal(AppTheme.Dark, _settings.Theme);
        Assert.Equal(AppTheme.Dark, _theme.LastApplied);
        Assert.Equal(1, _theme.ApplyCount);
    }

    [Fact]
    public void Choosing_the_current_theme_does_not_reapply()
    {
        SettingsViewModel page = Build();

        page.SelectedTheme = AppTheme.System;

        Assert.Equal(0, _theme.ApplyCount);
    }

    [Fact]
    public void Rejects_null_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(null!, _theme));
        Assert.Throws<ArgumentNullException>(() => new SettingsViewModel(_settings, null!));
    }
}
