using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows.Security;

/// <summary>
/// Keeps the product data folder (<see cref="WindowsDataPaths.Root"/>) and its
/// subfolders owned by Administrators, writable only by Administrators and SYSTEM,
/// and free of links. %ProgramData% lets any user create folders, so without this a
/// standard user could pre-create a subfolder (and own it), plant a journal or a
/// trusted key, or junction a folder elsewhere so the SYSTEM task writes a file of
/// their choosing.
/// <para>
/// A privileged process repairs each level from the root down: a link is removed, an
/// unsafe folder gets the protected ACL and Administrators as owner, and anything a
/// non-administrator planted inside is deleted (journals excepted — they are kept and
/// reported as tampered). An unprivileged process only verifies, and refuses an
/// unsafe folder. Locations outside the data folder are an operator's own choice and
/// are simply created.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsSecureDirectory : ISecureDirectory
{
    private static readonly ConcurrentDictionary<string, bool> Repaired = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _root;
    private readonly bool _privileged;
    private readonly Func<SecurityIdentifier, bool> _isTrusted;

    /// <summary>Guards the real data folder, repairing it when this process is elevated or SYSTEM.</summary>
    public WindowsSecureDirectory()
        : this(WindowsDataPaths.Root, IsPrivilegedProcess(), TrustedPrincipals.IsTrusted)
    {
    }

    /// <summary>Guards the data folder at <paramref name="root"/>, treating <paramref name="isTrusted"/> principals as administrators (tests).</summary>
    internal WindowsSecureDirectory(string root, bool privileged, Func<SecurityIdentifier, bool> isTrusted)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(isTrusted);
        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        _privileged = privileged;
        _isTrusted = isTrusted;
    }

    /// <summary>Whether this process runs elevated as an administrator or as SYSTEM.</summary>
    public static bool IsPrivilegedProcess()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.IsSystem || new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <inheritdoc />
    public void EnsureSafeForWrite(string directory)
    {
        string full = Full(directory);
        if (!IsUnderRoot(full))
        {
            Directory.CreateDirectory(full);
            return;
        }

        foreach (string level in Chain(full))
        {
            if (_privileged)
            {
                Repair(level);
            }
            else if (!IsSafeDirectory(level))
            {
                throw new UnauthorizedAccessException(string.Create(CultureInfo.InvariantCulture,
                    $"'{level}' is not secured for administrators; run any elevated Open the Windows command once to repair it."));
            }
        }
    }

    /// <summary>
    /// Whether the contents of <paramref name="directory"/> (under the data root) can be
    /// trusted for reading: every existing level from the root down is a real folder,
    /// owned by and writable only by administrators. A missing folder holds nothing.
    /// </summary>
    public bool IsTrustworthy(string directory)
    {
        string full = Full(directory);
        return !IsUnderRoot(full) || Chain(full).All(level => !Directory.Exists(level) || IsSafeDirectory(level));
    }

    /// <summary>Whether the file at <paramref name="path"/> is a real file owned by and writable only by administrators.</summary>
    public bool IsTrustedFile(string path)
    {
        var info = new FileInfo(path);
        return info.Exists
            && !info.Attributes.HasFlag(FileAttributes.ReparsePoint)
            && PathAcl.UntrustedHolders(info.GetAccessControl(), PathAcl.ContentRights | PathAcl.ControlRights, _isTrusted).Count == 0;
    }

    /// <summary>Whether no principal outside the trusted set can read the file at <paramref name="path"/> (for secrets).</summary>
    public bool IsPrivateFile(string path)
        => IsTrustedFile(path)
            && PathAcl.UntrustedHolders(new FileInfo(path).GetAccessControl(), FileSystemRights.ReadData, _isTrusted).Count == 0;

    private bool IsSafeDirectory(string path)
    {
        var info = new DirectoryInfo(path);
        return info.Exists
            && !info.Attributes.HasFlag(FileAttributes.ReparsePoint)
            && PathAcl.UntrustedHolders(info.GetAccessControl(), PathAcl.ContentRights | PathAcl.ControlRights, _isTrusted).Count == 0;
    }

    private void Repair(string level)
    {
        var info = new DirectoryInfo(level);
        if (info.Exists && info.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            if (string.Equals(level, _root, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException(string.Create(CultureInfo.InvariantCulture, $"'{level}' is a link; refusing to use it as the data folder."));
            }

            // Deleting a junction or directory symlink removes the link, never its target.
            Directory.Delete(level);
            info.Refresh();
        }

        DirectorySecurity desired = DesiredSecurity(UsersMayRead(level));
        if (!info.Exists)
        {
            info.Create(desired);
            Repaired[level] = true;
            return;
        }

        if (Repaired.ContainsKey(level) || IsSafeDirectory(level))
        {
            Repaired[level] = true;
            return;
        }

        info.SetAccessControl(desired);
        RemovePlantedChildren(info);
        Repaired[level] = true;
    }

    /// <summary>
    /// Removes what a non-administrator could have put in a folder before it was
    /// secured: links (the link, never its target) and files an untrusted account owns.
    /// Journals are kept (the store reports them as tampered and never replays them).
    /// Files an administrator owns get their permissions reset to inherit the folder's.
    /// Real subfolders are left alone: each one is verified, or repaired, at its own
    /// level, and resetting one here would hand the administrator-only keys folder the
    /// parent's read access.
    /// </summary>
    private void RemovePlantedChildren(DirectoryInfo folder)
    {
        bool keepUntrusted = string.Equals(folder.Name, WindowsDataPaths.JournalFolderName, StringComparison.OrdinalIgnoreCase);
        foreach (FileSystemInfo child in folder.EnumerateFileSystemInfos())
        {
            if (child.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                // Deleting a junction, directory symlink or file symlink removes the link only.
                child.Delete();
            }
            else if (child is FileInfo file)
            {
                bool untrusted = file.GetAccessControl().GetOwner(typeof(SecurityIdentifier)) is SecurityIdentifier owner && !_isTrusted(owner);
                if (!untrusted)
                {
                    ResetToInherited(file);
                }
                else if (!keepUntrusted)
                {
                    file.Delete();
                }
            }
        }
    }

    private static void ResetToInherited(FileInfo file)
    {
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: false, preserveInheritance: false);
        file.SetAccessControl(security);
    }

    private static DirectorySecurity DesiredSecurity(bool usersRead)
    {
        var security = new DirectorySecurity();
        security.SetOwner(TrustedPrincipals.Administrators);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        AddRule(security, TrustedPrincipals.LocalSystem, FileSystemRights.FullControl);
        AddRule(security, TrustedPrincipals.Administrators, FileSystemRights.FullControl);
        if (usersRead)
        {
            AddRule(security, TrustedPrincipals.Users, FileSystemRights.ReadAndExecute);
        }

        return security;
    }

    private static void AddRule(DirectorySecurity security, SecurityIdentifier sid, FileSystemRights rights)
        => security.AddAccessRule(new FileSystemAccessRule(
            sid,
            rights,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));

    private bool UsersMayRead(string level)
        => !string.Equals(level, Path.Combine(_root, WindowsDataPaths.KeysFolderName), StringComparison.OrdinalIgnoreCase);

    private bool IsUnderRoot(string full)
        => string.Equals(full, _root, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>The root, then each folder below it down to <paramref name="full"/>.</summary>
    private IEnumerable<string> Chain(string full)
    {
        yield return _root;
        string current = _root;
        foreach (string segment in full[_root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            yield return current;
        }
    }

    private static string Full(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
    }
}
