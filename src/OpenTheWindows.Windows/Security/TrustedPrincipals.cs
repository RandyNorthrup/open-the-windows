using System.ComponentModel;
using System.Runtime.Versioning;
using System.Security.Principal;
using OpenTheWindows.Windows.Interop;

namespace OpenTheWindows.Windows.Security;

/// <summary>
/// The principals whose control of a file or folder the product treats as
/// administrator control: SYSTEM, BUILTIN\Administrators, TrustedInstaller, and the
/// individual accounts that are direct members of the local Administrators group.
/// Placeholder SIDs (CREATOR OWNER, OWNER RIGHTS) never match a real token and are
/// treated as trusted. Everyone else — Users, Authenticated Users, a standard
/// account — is untrusted.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class TrustedPrincipals
{
    private const string TrustedInstallerSid = "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";

    private static readonly Lazy<HashSet<SecurityIdentifier>> AdministratorMembers = new(LoadAdministratorMembers);

    /// <summary>SYSTEM.</summary>
    public static SecurityIdentifier LocalSystem { get; } = new(WellKnownSidType.LocalSystemSid, null);

    /// <summary>BUILTIN\Administrators.</summary>
    public static SecurityIdentifier Administrators { get; } = new(WellKnownSidType.BuiltinAdministratorsSid, null);

    /// <summary>BUILTIN\Users.</summary>
    public static SecurityIdentifier Users { get; } = new(WellKnownSidType.BuiltinUsersSid, null);

    private static SecurityIdentifier TrustedInstaller { get; } = new(TrustedInstallerSid);

    private static SecurityIdentifier CreatorOwner { get; } = new(WellKnownSidType.CreatorOwnerSid, null);

    private static SecurityIdentifier OwnerRights { get; } = new("S-1-3-4");

    /// <summary>Whether <paramref name="sid"/> is administrator-controlled (or a placeholder).</summary>
    public static bool IsTrusted(SecurityIdentifier sid)
    {
        ArgumentNullException.ThrowIfNull(sid);
        return sid == LocalSystem || sid == Administrators || sid == TrustedInstaller
            || sid == CreatorOwner || sid == OwnerRights
            || AdministratorMembers.Value.Contains(sid);
    }

    private static HashSet<SecurityIdentifier> LoadAdministratorMembers()
    {
        try
        {
            return [.. LocalGroups.DirectMembers(Administrators)];
        }
        catch (Exception ex) when (ex is Win32Exception or IdentityNotMappedException)
        {
            // Unenumerable group: fall back to the well-known principals only (the safe direction).
            return [];
        }
    }
}
