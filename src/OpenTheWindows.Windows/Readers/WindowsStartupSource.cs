using System.Runtime.Versioning;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Startup;

namespace OpenTheWindows.Windows.Readers;

/// <summary>
/// <see cref="IStartupSource"/> over the registry and file system: the HKLM, 32-bit HKLM and
/// the signed-in user's Run values, and the all-users and the user's Startup folders (the
/// user's folder is found through their own hive, not this process's profile).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsStartupSource : IStartupSource
{
    /// <summary>The Run key, relative to HKLM or a user's hive.</summary>
    public const string RunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>The 32-bit Run key under HKLM.</summary>
    public const string Run32Path = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";

    private const string ShellFoldersPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders";
    private const string StartupFolderValue = "Startup";
    private const string DesktopIni = "desktop.ini";

    private readonly string _machineStartupFolder;

    /// <summary>Creates the source over the real all-users Startup folder.</summary>
    public WindowsStartupSource()
        : this(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup))
    {
    }

    /// <summary>Creates the source over a given all-users Startup folder (tests).</summary>
    internal WindowsStartupSource(string machineStartupFolder) => _machineStartupFolder = machineStartupFolder;

    /// <inheritdoc />
    public IReadOnlyList<StartupEntry> List(string? userSid)
    {
        List<StartupEntry> entries =
        [
            .. Run(null, RunPath, StartupLocation.MachineRun),
            .. Run(null, Run32Path, StartupLocation.MachineRun32),
            .. Folder(_machineStartupFolder, StartupLocation.MachineStartupFolder),
        ];
        if (!string.IsNullOrEmpty(userSid))
        {
            entries.AddRange(Run(userSid, RunPath, StartupLocation.UserRun));
            entries.AddRange(Folder(UserStartupFolder(userSid), StartupLocation.UserStartupFolder));
        }

        return entries;
    }

    /// <summary>The user's Startup folder as Explorer records it in their hive, or <see langword="null"/>.</summary>
    internal static string? UserStartupFolder(string userSid)
        => RegistryEnumeration.StringValues(userSid, ShellFoldersPath)
            .FirstOrDefault(v => string.Equals(v.Key, StartupFolderValue, StringComparison.OrdinalIgnoreCase)).Value;

    private static IEnumerable<StartupEntry> Run(string? userSid, string path, StartupLocation location)
        => RegistryEnumeration.StringValues(userSid, path).Select(v => new StartupEntry(location, v.Key, v.Value));

    private static IEnumerable<StartupEntry> Folder(string? folder, StartupLocation location)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return [];
        }

        return Directory.EnumerateFiles(folder)
            .Where(f => !string.Equals(Path.GetFileName(f), DesktopIni, StringComparison.OrdinalIgnoreCase))
            .Select(f => new StartupEntry(location, Path.GetFileName(f), f));
    }
}
