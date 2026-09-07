using System.IO;
using System.Text.Json;
using OpenTheWindows.Core;

namespace OpenTheWindows.App.Services;

/// <summary>
/// JSON file under LocalAppData. A missing or unreadable file is treated as the
/// factory defaults so a truncated write cannot block launch; the next Save
/// overwrites it.
/// </summary>
internal sealed class FileGuiSettingsStore : IGuiSettingsStore
{
    /// <summary>File name under the product LocalAppData directory.</summary>
    public const string FileName = "gui-settings.json";

    private readonly string _path;

    /// <summary>Creates a store at the default LocalAppData path.</summary>
    public FileGuiSettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppInfo.DataDirectoryName,
            FileName))
    {
    }

    /// <summary>Creates a store at <paramref name="path"/> (tests).</summary>
    public FileGuiSettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    /// <inheritdoc />
    public GuiSettingsSnapshot Load()
    {
        if (!File.Exists(_path))
        {
            return GuiSettingsSnapshot.Defaults;
        }

        try
        {
            string json = File.ReadAllText(_path);
            GuiSettingsSnapshot? snapshot = JsonSerializer.Deserialize(json, GuiSettingsJsonContext.Default.GuiSettingsSnapshot);
            return snapshot ?? GuiSettingsSnapshot.Defaults;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return GuiSettingsSnapshot.Defaults;
        }
    }

    /// <inheritdoc />
    public void Save(GuiSettingsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string? directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(snapshot, GuiSettingsJsonContext.Default.GuiSettingsSnapshot);
        string temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, _path, overwrite: true);
    }
}
