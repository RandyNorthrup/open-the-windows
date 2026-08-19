namespace OpenTheWindows.App.Services;

/// <summary>
/// Prompts the user to pick a profile file to open or a path to save one. The
/// UI seam behind import/export; a fake returns scripted paths in tests.
/// </summary>
internal interface IFileDialogService
{
    /// <summary>Asks for an existing profile <c>.json</c> file; returns its path, or <see langword="null"/> if cancelled.</summary>
    string? OpenProfile();

    /// <summary>Asks where to save a profile, seeded with <paramref name="suggestedFileName"/>; returns the path, or <see langword="null"/> if cancelled.</summary>
    string? SaveProfile(string suggestedFileName);
}
