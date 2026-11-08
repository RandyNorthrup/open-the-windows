using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace OpenTheWindows.Windows.Interop;

/// <summary>
/// Remote Desktop Services session queries: which session holds the interactive user
/// (this process's own desktop session, or — for a SYSTEM service or an SSH logon in
/// session 0 — the active console session), the user signed in to a session, and the
/// session a given user SID is signed in to.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class WtsSessions
{
    private static readonly nint CurrentServer = nint.Zero;
    private const uint NoConsoleSession = 0xFFFFFFFF;
    private const uint ServicesSession = 0;
    private const uint EnumerateVersion = 1;
    private const int WtsUserName = 5;
    private const int WtsDomainName = 7;
    private const int WtsActive = 0;
    private const int WtsDisconnected = 4;

    /// <summary>
    /// The session a per-user change should target: this process's own session when a user
    /// is signed in to it; otherwise the active console session; <see langword="null"/>
    /// when nobody is signed in at the console either.
    /// </summary>
    public static uint? InteractiveSessionId()
    {
        if (ProcessIdToSessionId(GetCurrentProcessId(), out uint own) && own != ServicesSession && !string.IsNullOrEmpty(Query(own, WtsUserName)))
        {
            return own;
        }

        uint console = WTSGetActiveConsoleSessionId();
        return console is NoConsoleSession or ServicesSession ? null : console;
    }

    /// <summary>The domain and user name signed in to <paramref name="session"/> (empty values when nobody is).</summary>
    public static (string? Domain, string? UserName) User(uint session) => (Query(session, WtsDomainName), Query(session, WtsUserName));

    /// <summary>The SID of <paramref name="domain"/>\<paramref name="userName"/>, or <see langword="null"/> when it cannot be mapped.</summary>
    public static string? SidFor(string? domain, string userName)
    {
        NTAccount account = string.IsNullOrEmpty(domain) ? new NTAccount(userName) : new NTAccount(domain, userName);
        try
        {
            return ((SecurityIdentifier)account.Translate(typeof(SecurityIdentifier))).Value;
        }
        catch (IdentityNotMappedException)
        {
            return null;
        }
    }

    /// <summary>The session <paramref name="sid"/> is signed in to (an active one first), or <see langword="null"/>.</summary>
    public static uint? SessionForSid(string sid)
    {
        if (!WTSEnumerateSessions(CurrentServer, 0, EnumerateVersion, out nint buffer, out uint count) || buffer == nint.Zero)
        {
            return null;
        }

        try
        {
            int size = Marshal.SizeOf<SessionInfo>();
            List<SessionInfo> sessions = [];
            for (int i = 0; i < count; i++)
            {
                sessions.Add(Marshal.PtrToStructure<SessionInfo>(buffer + (i * size)));
            }

            return sessions
                .Where(s => s.State is WtsActive or WtsDisconnected)
                .OrderBy(s => s.State == WtsActive ? 0 : 1)
                .Select(s => (uint?)s.SessionId)
                .FirstOrDefault(id => User(id!.Value) is { UserName: { Length: > 0 } name } user
                    && string.Equals(SidFor(user.Domain, name), sid, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    private static string? Query(uint session, int infoClass)
    {
        if (!WTSQuerySessionInformation(CurrentServer, session, infoClass, out nint buffer, out _) || buffer == nint.Zero)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUni(buffer);
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SessionInfo
    {
        public uint SessionId;
        public nint WinStationName;
        public int State;
    }

    [LibraryImport("wtsapi32.dll", EntryPoint = "WTSQuerySessionInformationW", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WTSQuerySessionInformation(nint server, uint sessionId, int infoClass, out nint buffer, out uint bytesReturned);

    [LibraryImport("wtsapi32.dll", EntryPoint = "WTSEnumerateSessionsW", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WTSEnumerateSessions(nint server, uint reserved, uint version, out nint sessions, out uint count);

    [LibraryImport("wtsapi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial void WTSFreeMemory(nint memory);

    [LibraryImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial uint WTSGetActiveConsoleSessionId();

    [LibraryImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial uint GetCurrentProcessId();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ProcessIdToSessionId(uint processId, out uint sessionId);
}
