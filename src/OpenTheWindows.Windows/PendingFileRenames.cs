namespace OpenTheWindows.Windows;

/// <summary>
/// Reads Session Manager's <c>PendingFileRenameOperations</c> (pairs of source and
/// destination left by <c>MoveFileEx(MOVEFILE_DELAY_UNTIL_REBOOT)</c>) for whether a
/// restart is really owed. Installers and uninstallers routinely queue their own
/// temporary files for deletion at the next restart; that changes nothing about
/// Windows, so it does not count. A rename (a file in use waiting to be replaced) or
/// a deletion anywhere outside a temporary folder does.
/// </summary>
internal static class PendingFileRenames
{
    // "\??\C:\..." is the NT path form MoveFileEx records; some writers put a marker (e.g. "*1") before it.
    private const string NtPathPrefix = @"\??\";

    private const string TempFolderName = "Temp";

    private const string SystemTempFolderName = "SystemTemp";

    private static readonly string WindowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    // The machine's temporary folders, each ending in a separator so "...\TempX" is not taken for "...\Temp".
    private static readonly string[] MachineTemporaryFolders =
    [
        Path.Join(WindowsFolder, TempFolderName) + Path.DirectorySeparatorChar,
        Path.Join(WindowsFolder, SystemTempFolderName) + Path.DirectorySeparatorChar,
    ];

    // A user's temporary folder, in whichever profile: "\AppData\Local\Temp\".
    private static readonly string UserTemporaryFolder =
        Path.DirectorySeparatorChar
        + Path.Join(
            Path.GetRelativePath(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
            TempFolderName)
        + Path.DirectorySeparatorChar;

    /// <summary>Whether the queued operations include one that changes the system at the next restart.</summary>
    public static bool RequireReboot(IReadOnlyList<string> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        for (int i = 0; i < operations.Count; i += 2)
        {
            string source = operations[i];
            string destination = i + 1 < operations.Count ? operations[i + 1] : string.Empty;
            if (source.Length == 0)
            {
                continue;
            }

            if (destination.Length > 0 || !IsTemporary(source))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTemporary(string source)
    {
        int start = source.IndexOf(NtPathPrefix, StringComparison.Ordinal);
        string path = start >= 0 ? source[(start + NtPathPrefix.Length)..] : source;
        if (!Path.IsPathFullyQualified(path))
        {
            return false;
        }

        // Resolve "..\" so a path that only passes through a temporary folder is not taken for one inside it.
        string full = Path.GetFullPath(path);
        return full.Contains(UserTemporaryFolder, StringComparison.OrdinalIgnoreCase)
            || MachineTemporaryFolders.Any(folder => full.StartsWith(folder, StringComparison.OrdinalIgnoreCase));
    }
}
