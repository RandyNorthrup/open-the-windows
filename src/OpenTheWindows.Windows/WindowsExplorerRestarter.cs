using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows;

/// <summary>
/// Restarts <c>explorer.exe</c> so shell/taskbar settings take effect without a
/// sign-out, by terminating the shell processes in the session the target user is
/// signed in to (an RDP or fast-user-switched session included, so another user's
/// desktop is never touched); Winlogon relaunches the shell automatically.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsExplorerRestarter : IExplorerRestarter
{
    private const uint Th32csSnapProcess = 0x00000002;
    private const uint ProcessTerminate = 0x0001;
    private const string ExplorerImage = "explorer.exe";
    private static readonly nint InvalidHandle = new(-1);

    /// <inheritdoc />
    public void RestartForSession(string userSid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userSid);

        if (Interop.WtsSessions.SessionForSid(userSid) is not { } session)
        {
            // The user is not signed in anywhere: there is no shell to restart.
            return;
        }

        foreach (uint pid in ExplorerProcessIds(session))
        {
            Terminate(pid);
        }
    }

    private static List<uint> ExplorerProcessIds(uint session)
    {
        List<uint> pids = [];
        nint snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
        if (snapshot == InvalidHandle)
        {
            return pids;
        }

        try
        {
            var entry = new ProcessEntry32 { Size = (uint)Marshal.SizeOf<ProcessEntry32>() };
            for (bool more = Process32FirstW(snapshot, ref entry); more; more = Process32NextW(snapshot, ref entry))
            {
                if (string.Equals(entry.ExeFile, ExplorerImage, StringComparison.OrdinalIgnoreCase)
                    && ProcessIdToSessionId(entry.ProcessId, out uint processSession)
                    && processSession == session)
                {
                    pids.Add(entry.ProcessId);
                }
            }
        }
        finally
        {
            _ = CloseHandle(snapshot);
        }

        return pids;
    }

    private static void Terminate(uint pid)
    {
        nint handle = OpenProcess(ProcessTerminate, bInheritHandle: false, pid);
        if (handle == nint.Zero)
        {
            return;
        }

        try
        {
            _ = TerminateProcess(handle, 1);
        }
        finally
        {
            _ = CloseHandle(handle);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public nint DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityClassBase;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32FirstW(nint snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32NextW(nint snapshot, ref ProcessEntry32 entry);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ProcessIdToSessionId(uint processId, out uint sessionId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TerminateProcess(nint process, uint exitCode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);
}
