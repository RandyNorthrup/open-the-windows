using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using Microsoft.Win32;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows.Readers;

/// <summary>
/// Resolves the interactive (console/RDP) session's logged-on user through the
/// Terminal Services API (<c>WTSQuerySessionInformation</c>) — never the process
/// token, which for an elevated app is the elevating administrator rather than
/// the signed-in user whose per-user hive must be targeted. Read-only.
/// </summary>
/// <remarks>
/// The user name and domain come from <c>WTS_CURRENT_SESSION</c>; the SID is
/// resolved from them with the managed <see cref="NTAccount"/> translation (no
/// <c>LookupAccountName</c> P/Invoke needed). <see cref="InteractiveUser.HiveLoaded"/>
/// reflects whether <c>HKEY_USERS\&lt;sid&gt;</c> is present. When there is no
/// interactive user (e.g. a session-0 service) or the account cannot be mapped
/// to a SID, the resolver returns <see langword="null"/>. Declared with the
/// <see cref="LibraryImportAttribute"/> source generator (see PLAN §7.1).
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsInteractiveUserResolver : IInteractiveUserResolver
{
    private const uint WtsCurrentSession = 0xFFFFFFFF;
    private const int WtsUserName = 5;
    private const int WtsDomainName = 7;

    /// <inheritdoc />
    public InteractiveUser? Resolve()
    {
        string? userName = QuerySessionString(WtsUserName);
        if (string.IsNullOrEmpty(userName))
        {
            return null;
        }

        string? domain = QuerySessionString(WtsDomainName);
        string? sid = TranslateToSid(domain, userName);
        if (sid is null)
        {
            return null;
        }

        return new InteractiveUser(sid, Format(domain, userName), IsHiveLoaded(sid));
    }

    private static string Format(string? domain, string userName)
    {
        return string.IsNullOrEmpty(domain) ? userName : $@"{domain}\{userName}";
    }

    private static string? TranslateToSid(string? domain, string userName)
    {
        NTAccount account = string.IsNullOrEmpty(domain)
            ? new NTAccount(userName)
            : new NTAccount(domain, userName);
        try
        {
            return ((SecurityIdentifier)account.Translate(typeof(SecurityIdentifier))).Value;
        }
        catch (IdentityNotMappedException)
        {
            return null;
        }
    }

    private static bool IsHiveLoaded(string sid)
    {
        using var users = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64);
        using RegistryKey? hive = users.OpenSubKey(sid, writable: false);
        return hive is not null;
    }

    private static string? QuerySessionString(int infoClass)
    {
        if (!WTSQuerySessionInformation(nint.Zero, WtsCurrentSession, infoClass, out nint buffer, out _)
            || buffer == nint.Zero)
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

    [LibraryImport("wtsapi32.dll", EntryPoint = "WTSQuerySessionInformationW", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WTSQuerySessionInformation(
        nint server, uint sessionId, int infoClass, out nint buffer, out uint bytesReturned);

    [LibraryImport("wtsapi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial void WTSFreeMemory(nint memory);
}
