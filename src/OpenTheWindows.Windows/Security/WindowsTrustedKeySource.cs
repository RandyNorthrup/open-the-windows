using System.Runtime.Versioning;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Windows.Security;

/// <summary>
/// The machine trust store (<c>%ProgramData%\OpenTheWindows\trusted-keys</c>). A key
/// counts only when the folder chain is administrator-owned and administrator-writable
/// and the key file itself is owned by and writable only by administrators, so a
/// standard user cannot add a signer and defeat <c>RequireSignedProfiles</c>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsTrustedKeySource : ITrustedKeySource
{
    private readonly WindowsSecureDirectory _directories;

    /// <summary>Reads the machine trust store.</summary>
    public WindowsTrustedKeySource()
        : this(WindowsDataPaths.TrustedKeys, new WindowsSecureDirectory())
    {
    }

    internal WindowsTrustedKeySource(string directory, WindowsSecureDirectory directories)
    {
        Description = directory;
        _directories = directories;
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> ReadPublicKeysPem()
    {
        if (!Directory.Exists(Description) || !_directories.IsTrustworthy(Description))
        {
            return [];
        }

        return DirectoryTrustedKeySource.ReadPemFiles(
            Directory.EnumerateFiles(Description, DirectoryTrustedKeySource.KeyFilePattern).Where(_directories.IsTrustedFile));
    }
}
