using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows.Writers;

/// <summary>
/// Loads and unloads a user's registry hive with <c>RegLoadKey</c>/<c>RegUnLoadKey</c>,
/// enabling the backup and restore privileges those calls require. Used by an
/// all-users apply to reach the hive of a user who is not logged on; the hive is
/// mounted under <c>HKEY_USERS\&lt;mountKey&gt;</c> so the existing per-user
/// registry writer (which targets <c>HKU\&lt;sid&gt;</c>) writes to it unchanged.
/// Callers must <see cref="Unload"/> in a <see langword="finally"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsUserHiveLoader : IUserHiveLoader
{
    // Predefined key handle HKEY_USERS (0x80000003), zero-extended to a 64-bit handle.
    private static readonly nint HkeyUsers = unchecked((nint)0x80000003);

    private const uint TokenAdjustPrivileges = 0x0020;
    private const uint TokenQuery = 0x0008;
    private const uint SePrivilegeEnabled = 0x00000002;
    private const string SeBackupPrivilege = "SeBackupPrivilege";
    private const string SeRestorePrivilege = "SeRestorePrivilege";

    /// <inheritdoc />
    public void Load(string mountKey, string hivePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mountKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(hivePath);

        EnablePrivilege(SeBackupPrivilege);
        EnablePrivilege(SeRestorePrivilege);

        int status = RegLoadKey(HkeyUsers, mountKey, hivePath);
        if (status != 0)
        {
            throw new Win32Exception(status, $"RegLoadKey failed for '{hivePath}' at HKU\\{mountKey}.");
        }
    }

    /// <inheritdoc />
    public void Unload(string mountKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mountKey);

        int status = RegUnLoadKey(HkeyUsers, mountKey);
        if (status != 0)
        {
            throw new Win32Exception(status, $"RegUnLoadKey failed for HKU\\{mountKey}.");
        }
    }

    private static void EnablePrivilege(string name)
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out nint token))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenProcessToken failed.");
        }

        try
        {
            if (!LookupPrivilegeValue(null, name, out Luid luid))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"LookupPrivilegeValue failed for {name}.");
            }

            var privileges = new TokenPrivileges
            {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = SePrivilegeEnabled,
            };

            if (!AdjustTokenPrivileges(token, false, ref privileges, 0, nint.Zero, nint.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"AdjustTokenPrivileges failed for {name}.");
            }

            // AdjustTokenPrivileges reports success even when the privilege was not held;
            // the last error is ERROR_NOT_ALL_ASSIGNED (1300) in that case. The process
            // must actually hold the privilege (an elevated or SYSTEM token does).
            int lastError = Marshal.GetLastWin32Error();
            if (lastError != 0)
            {
                throw new Win32Exception(lastError, $"The process does not hold {name} (run elevated or as SYSTEM).");
            }
        }
        finally
        {
            CloseHandle(token);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenPrivileges
    {
        public uint PrivilegeCount;
        public Luid Luid;
        public uint Attributes;
    }

    [LibraryImport("advapi32.dll", EntryPoint = "RegLoadKeyW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int RegLoadKey(nint hKey, string subKey, string file);

    [LibraryImport("advapi32.dll", EntryPoint = "RegUnLoadKeyW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int RegUnLoadKey(nint hKey, string subKey);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenProcessToken(nint process, uint desiredAccess, out nint token);

    [LibraryImport("advapi32.dll", EntryPoint = "LookupPrivilegeValueW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool LookupPrivilegeValue(string? systemName, string name, out Luid luid);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AdjustTokenPrivileges(
        nint token, [MarshalAs(UnmanagedType.Bool)] bool disableAll, ref TokenPrivileges newState,
        uint bufferLength, nint previousState, nint returnLength);

    [LibraryImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);
}
