using System.IO;
using OpenTheWindows.App.Services;
using OpenTheWindows.App.ViewModels;

namespace OpenTheWindows.App.Tests.Services;

public sealed class FileGuiSettingsStoreTests
{
    [Fact]
    public void Missing_file_loads_defaults()
    {
        string path = Path.Combine(Path.GetTempPath(), "otw-gui-settings-missing", Guid.NewGuid().ToString("N"), "gui-settings.json");
        var store = new FileGuiSettingsStore(path);

        GuiSettingsSnapshot snapshot = store.Load();

        Assert.Equal(GuiSettingsSnapshot.Defaults, snapshot);
    }

    [Fact]
    public void Round_trips_a_snapshot()
    {
        string path = Path.Combine(Path.GetTempPath(), "otw-gui-settings-roundtrip", Guid.NewGuid().ToString("N"), "gui-settings.json");
        var store = new FileGuiSettingsStore(path);
        var written = new GuiSettingsSnapshot(GuiSettingsSnapshot.CurrentSchemaVersion, false, true, true, AppTheme.Dark);

        store.Save(written);
        GuiSettingsSnapshot loaded = store.Load();

        Assert.Equal(written, loaded);
    }

    [Fact]
    public void Unreadable_file_loads_defaults()
    {
        string path = Path.Combine(Path.GetTempPath(), "otw-gui-settings-bad", Guid.NewGuid().ToString("N"), "gui-settings.json");
        string? directory = Path.GetDirectoryName(path);
        Assert.NotNull(directory);
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, "{not json");
        var store = new FileGuiSettingsStore(path);

        GuiSettingsSnapshot snapshot = store.Load();

        Assert.Equal(GuiSettingsSnapshot.Defaults, snapshot);
    }
}
