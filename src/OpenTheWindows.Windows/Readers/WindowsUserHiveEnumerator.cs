using System.Runtime.Versioning;
using Microsoft.Win32;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows.Readers;

/// <summary>
/// Enumerates real user profiles from
/// <c>HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList</c>. Read-only.
/// Keeps only real user accounts — local or domain (<c>S-1-5-21-…</c>) and Azure AD
/// (<c>S-1-12-1-…</c>) — and skips the service accounts (LocalSystem/LocalService/
/// NetworkService), the <c>.DEFAULT</c> profile and any other non-account SID. A
/// profile is reported <see cref="UserProfile.HiveLoaded"/> when
/// <c>HKEY_USERS\&lt;sid&gt;</c> is present.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsUserHiveEnumerator : IUserHiveEnumerator
{
    private const string ProfileListPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList";
    private const string ProfileImagePathValue = "ProfileImagePath";

    /// <inheritdoc />
    public IReadOnlyList<UserProfile> Enumerate()
    {
        using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using RegistryKey? profileList = machine.OpenSubKey(ProfileListPath, writable: false);
        if (profileList is null)
        {
            return [];
        }

        using var users = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64);

        var profiles = new List<UserProfile>();
        foreach (string sid in profileList.GetSubKeyNames())
        {
            if (!IsRealUserSid(sid))
            {
                continue;
            }

            using RegistryKey? entry = profileList.OpenSubKey(sid, writable: false);
            if (entry?.GetValue(ProfileImagePathValue) is not string profilePath || profilePath.Length == 0)
            {
                continue;
            }

            using RegistryKey? loadedHive = users.OpenSubKey(sid, writable: false);
            profiles.Add(new UserProfile(sid, Environment.ExpandEnvironmentVariables(profilePath), loadedHive is not null));
        }

        profiles.Sort(static (a, b) => string.CompareOrdinal(a.Sid, b.Sid));
        return profiles;
    }

    // Real accounts are S-1-5-21-<authority>-<rid> (local + AD) or S-1-12-1-<...> (Azure AD).
    // Everything else in ProfileList (S-1-5-18/19/20 service accounts, .DEFAULT) is not a real user.
    private static bool IsRealUserSid(string sid)
        => sid.StartsWith("S-1-5-21-", StringComparison.OrdinalIgnoreCase)
            || sid.StartsWith("S-1-12-1-", StringComparison.OrdinalIgnoreCase);
}
