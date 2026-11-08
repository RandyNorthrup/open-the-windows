using System.Runtime.Versioning;
using Microsoft.Win32;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Windows.Interop;

namespace OpenTheWindows.Windows.Readers;

/// <summary>
/// Resolves the interactive user through the Remote Desktop Services session API —
/// never the process token, which for an elevated app is the elevating administrator
/// rather than the signed-in user whose per-user hive must be targeted. Read-only.
/// </summary>
/// <remarks>
/// The session is this process's own desktop session; when the process has none (the
/// SYSTEM remediation task, or an SSH logon, both in session 0) it is the active console
/// session, so per-user settings still reach the person at the machine. The SID is
/// resolved from the session's domain and user name; <see cref="InteractiveUser.HiveLoaded"/>
/// reflects whether <c>HKEY_USERS\&lt;sid&gt;</c> is present. When nobody is signed in
/// at all, or the account cannot be mapped to a SID, the resolver returns
/// <see langword="null"/> and the engine treats per-user entries as not applicable.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsInteractiveUserResolver : IInteractiveUserResolver
{
    /// <inheritdoc />
    public InteractiveUser? Resolve()
    {
        if (WtsSessions.InteractiveSessionId() is not { } session)
        {
            return null;
        }

        (string? domain, string? userName) = WtsSessions.User(session);
        if (string.IsNullOrEmpty(userName))
        {
            return null;
        }

        string? sid = WtsSessions.SidFor(domain, userName);
        return sid is null ? null : new InteractiveUser(sid, Format(domain, userName), IsHiveLoaded(sid));
    }

    private static string Format(string? domain, string userName)
        => string.IsNullOrEmpty(domain) ? userName : $@"{domain}\{userName}";

    private static bool IsHiveLoaded(string sid)
    {
        using var users = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64);
        using RegistryKey? hive = users.OpenSubKey(sid, writable: false);
        return hive is not null;
    }
}
