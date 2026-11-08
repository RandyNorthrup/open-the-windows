using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace OpenTheWindows.Windows.Security;

/// <summary>
/// Evaluates a file or folder security descriptor for principals outside
/// <see cref="TrustedPrincipals"/> that could change or replace the object. Pure over
/// an in-memory <see cref="FileSystemSecurity"/>, so the rules are unit-tested
/// without touching the disk.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class PathAcl
{
    /// <summary>Rights that add or change content: writing a file, or adding files and folders to a folder.</summary>
    public const FileSystemRights ContentRights = FileSystemRights.WriteData | FileSystemRights.AppendData;

    /// <summary>Rights that let a principal delete, rename, re-permission or take over the object.</summary>
    public const FileSystemRights ControlRights = FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles
        | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;

    private const int GenericAll = 0x10000000;
    private const int GenericWrite = 0x40000000;

    /// <summary>
    /// The untrusted principals that own the object or hold any of
    /// <paramref name="dangerous"/> on it (inherit-only and deny entries are ignored —
    /// they do not grant access to this object). Empty when the object is safe.
    /// </summary>
    public static IReadOnlyList<SecurityIdentifier> UntrustedHolders(
        FileSystemSecurity security, FileSystemRights dangerous, Func<SecurityIdentifier, bool> isTrusted)
    {
        ArgumentNullException.ThrowIfNull(security);
        ArgumentNullException.ThrowIfNull(isTrusted);

        List<SecurityIdentifier> holders = [];
        if (security.GetOwner(typeof(SecurityIdentifier)) is SecurityIdentifier owner && !isTrusted(owner))
        {
            holders.Add(owner);
        }

        foreach (FileSystemAccessRule rule in security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
                     .OfType<FileSystemAccessRule>())
        {
            if (rule.AccessControlType != AccessControlType.Allow
                || rule.PropagationFlags.HasFlag(PropagationFlags.InheritOnly)
                || rule.IdentityReference is not SecurityIdentifier sid
                || isTrusted(sid))
            {
                continue;
            }

            int raw = (int)rule.FileSystemRights;
            if ((raw & (int)dangerous) != 0 || (raw & (GenericAll | GenericWrite)) != 0)
            {
                holders.Add(sid);
            }
        }

        return holders;
    }
}
