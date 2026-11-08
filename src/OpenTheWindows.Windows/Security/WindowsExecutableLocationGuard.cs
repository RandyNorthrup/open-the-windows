using System.Globalization;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows.Security;

/// <summary>
/// Walks an executable's path and refuses it when a non-administrator could
/// tamper with what would run: change or replace the file itself, add files to its
/// folder (DLL planting beside the executable), or delete, rename or re-permission
/// any folder above it. <c>%ProgramFiles%</c> (the MSI install location) passes; a
/// portable copy under Downloads or a folder made directly under <c>C:\</c> (which
/// grants Authenticated Users modify) does not.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsExecutableLocationGuard : IExecutableLocationGuard
{
    private const FileSystemRights FileRights = PathAcl.ContentRights | PathAcl.ControlRights;
    private const FileSystemRights FolderRights = PathAcl.ContentRights | PathAcl.ControlRights;
    private const FileSystemRights AncestorRights = PathAcl.ControlRights;

    private readonly Func<SecurityIdentifier, bool> _isTrusted;

    /// <summary>Treats administrators, SYSTEM and TrustedInstaller as the only trusted principals.</summary>
    public WindowsExecutableLocationGuard()
        : this(TrustedPrincipals.IsTrusted)
    {
    }

    /// <summary>Treats <paramref name="isTrusted"/> principals as administrators (tests).</summary>
    internal WindowsExecutableLocationGuard(Func<SecurityIdentifier, bool> isTrusted)
        => _isTrusted = isTrusted ?? throw new ArgumentNullException(nameof(isTrusted));

    /// <inheritdoc />
    public string? FindWeakness(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        var file = new FileInfo(Path.GetFullPath(executablePath));
        if (!file.Exists)
        {
            return string.Create(CultureInfo.InvariantCulture, $"'{file.FullName}' does not exist.");
        }

        if (Weakness(file.FullName, file.Attributes, file.GetAccessControl(), FileRights) is { } fileWeakness)
        {
            return fileWeakness;
        }

        DirectoryInfo? folder = file.Directory;
        FileSystemRights rights = FolderRights;
        while (folder is not null)
        {
            if (Weakness(folder.FullName, folder.Attributes, folder.GetAccessControl(), rights) is { } folderWeakness)
            {
                return folderWeakness;
            }

            rights = AncestorRights;
            folder = folder.Parent;
        }

        return null;
    }

    private string? Weakness(string path, FileAttributes attributes, FileSystemSecurity security, FileSystemRights dangerous)
    {
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return string.Create(CultureInfo.InvariantCulture, $"'{path}' is a link, so what it points to can change.");
        }

        IReadOnlyList<SecurityIdentifier> holders = PathAcl.UntrustedHolders(security, dangerous, _isTrusted);
        if (holders.Count == 0)
        {
            return null;
        }

        string who = string.Join(", ", holders.Distinct().Select(Describe));
        return string.Create(CultureInfo.InvariantCulture,
            $"'{path}' can be changed by non-administrators ({who}); install Open the Windows with the MSI (Program Files) or copy it to an administrator-only folder first.");
    }

    private static string Describe(SecurityIdentifier sid)
    {
        try
        {
            return sid.Translate(typeof(NTAccount)).Value;
        }
        catch (IdentityNotMappedException)
        {
            return sid.Value;
        }
    }
}
