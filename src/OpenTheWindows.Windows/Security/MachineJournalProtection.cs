using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace OpenTheWindows.Windows.Security;

/// <summary>
/// Protection for the machine journal under %ProgramData%: the folder chain is kept
/// administrator-owned and administrator-writable (<see cref="WindowsSecureDirectory"/>),
/// each journal file must be owned by an administrator or SYSTEM, and every record
/// carries an HMAC under a 256-bit key that only administrators and SYSTEM can read.
/// A standard user therefore cannot plant or alter a journal that an elevated revert
/// would replay.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class MachineJournalProtection : IJournalProtection
{
    private const int KeyBytes = 32;
    private const string KeyFileName = "journal.key";

    private readonly WindowsSecureDirectory _directories;
    private readonly string _keyDirectory;
    private byte[]? _key;

    public MachineJournalProtection()
        : this(new WindowsSecureDirectory(), WindowsDataPaths.Keys)
    {
    }

    internal MachineJournalProtection(WindowsSecureDirectory directories, string keyDirectory)
    {
        _directories = directories;
        _keyDirectory = keyDirectory;
    }

    /// <inheritdoc />
    public void PrepareForWrite(string directory) => _directories.EnsureSafeForWrite(directory);

    /// <inheritdoc />
    public bool IsTrustworthy(string directory) => _directories.IsTrustworthy(directory);

    /// <inheritdoc />
    public bool IsTrustedFile(string path) => _directories.IsTrustedFile(path);

    /// <inheritdoc />
    public byte[]? Key(bool create)
    {
        if (_key is not null)
        {
            return _key;
        }

        string path = Path.Combine(_keyDirectory, KeyFileName);
        try
        {
            if (create)
            {
                _directories.EnsureSafeForWrite(_keyDirectory);
            }

            if (File.Exists(path) && _directories.IsPrivateFile(path))
            {
                byte[] existing = File.ReadAllBytes(path);
                if (existing.Length == KeyBytes)
                {
                    _key = existing;
                    return _key;
                }
            }

            if (!create)
            {
                return null;
            }

            byte[] fresh = RandomNumberGenerator.GetBytes(KeyBytes);
            File.WriteAllBytes(path, fresh);
            _key = fresh;
            return _key;
        }
        catch (UnauthorizedAccessException) when (!create)
        {
            // An unelevated reader cannot open the key; records are then listed as unverified.
            return null;
        }
    }
}
