namespace OpenTheWindows.App.Services;

/// <summary>Loads and saves the GUI preferences file.</summary>
internal interface IGuiSettingsStore
{
    /// <summary>Reads the snapshot. Missing or unreadable files yield <see cref="GuiSettingsSnapshot.Defaults"/>.</summary>
    GuiSettingsSnapshot Load();

    /// <summary>Writes <paramref name="snapshot"/> atomically.</summary>
    void Save(GuiSettingsSnapshot snapshot);
}
