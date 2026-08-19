using Microsoft.Win32;

namespace OpenTheWindows.App.Services;

/// <summary>
/// <see cref="IFileDialogService"/> over the WPF common file dialogs. The
/// production implementation; tests use a fake that returns scripted paths.
/// </summary>
internal sealed class FileDialogService : IFileDialogService
{
    private const string ProfileFilter = "Profiles (*.json)|*.json|All files (*.*)|*.*";

    /// <inheritdoc />
    public string? OpenProfile()
    {
        OpenFileDialog dialog = new()
        {
            Title = "Import profile",
            Filter = ProfileFilter,
            CheckFileExists = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <inheritdoc />
    public string? SaveProfile(string suggestedFileName)
    {
        SaveFileDialog dialog = new()
        {
            Title = "Export profile",
            Filter = ProfileFilter,
            FileName = suggestedFileName,
            AddExtension = true,
            DefaultExt = ".json",
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
