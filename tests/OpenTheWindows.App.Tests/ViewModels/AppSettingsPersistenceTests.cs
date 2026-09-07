using System.IO;
using OpenTheWindows.App.Services;
using OpenTheWindows.App.ViewModels;

namespace OpenTheWindows.App.Tests.ViewModels;

public sealed class AppSettingsPersistenceTests
{
    [Fact]
    public void Parameterless_constructor_does_not_need_a_store()
    {
        var settings = new AppSettings { Theme = AppTheme.Light };

        Assert.Equal(AppTheme.Light, settings.Theme);
    }

    [Fact]
    public void Store_backed_settings_load_and_save()
    {
        string path = Path.Combine(Path.GetTempPath(), "otw-appsettings", Guid.NewGuid().ToString("N"), "gui-settings.json");
        var store = new FileGuiSettingsStore(path);
        store.Save(new GuiSettingsSnapshot(GuiSettingsSnapshot.CurrentSchemaVersion, false, true, true, AppTheme.Dark));

        var settings = new AppSettings(store);

        Assert.False(settings.CreateRestorePoint);
        Assert.True(settings.RestartExplorerAutomatically);
        Assert.True(settings.EnterpriseMode);
        Assert.Equal(AppTheme.Dark, settings.Theme);

        settings.Theme = AppTheme.Light;

        Assert.Equal(AppTheme.Light, store.Load().Theme);
    }

    [Fact]
    public void Rejects_a_null_store()
        => Assert.Throws<ArgumentNullException>(() => new AppSettings(null!));
}
