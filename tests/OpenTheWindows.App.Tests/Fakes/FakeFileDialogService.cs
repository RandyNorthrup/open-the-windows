using OpenTheWindows.App.Services;

namespace OpenTheWindows.App.Tests.Fakes;

/// <summary>Scriptable <see cref="IFileDialogService"/>: returns the paths a test sets (null = cancelled).</summary>
internal sealed class FakeFileDialogService : IFileDialogService
{
    /// <summary>The path <see cref="OpenProfile"/> returns.</summary>
    public string? OpenPath { get; set; }

    /// <summary>The path <see cref="SaveProfile"/> returns.</summary>
    public string? SavePath { get; set; }

    /// <summary>The suggested file name passed to the last <see cref="SaveProfile"/> call.</summary>
    public string? LastSuggestedName { get; private set; }

    /// <inheritdoc />
    public string? OpenProfile() => OpenPath;

    /// <inheritdoc />
    public string? SaveProfile(string suggestedFileName)
    {
        LastSuggestedName = suggestedFileName;
        return SavePath;
    }
}
