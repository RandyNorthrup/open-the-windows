namespace OpenTheWindows.Windows.Security;

/// <summary>
/// No folder hardening and no key: records carry a plain hash chain. Used for the
/// per-user journal under the caller's own profile (no privilege boundary to defend)
/// and for tests against a temporary folder.
/// </summary>
internal sealed class NoJournalProtection : IJournalProtection
{
    /// <summary>The shared instance.</summary>
    public static NoJournalProtection Instance { get; } = new();

    /// <inheritdoc />
    public void PrepareForWrite(string directory) => Directory.CreateDirectory(directory);

    /// <inheritdoc />
    public bool IsTrustworthy(string directory) => true;

    /// <inheritdoc />
    public bool IsTrustedFile(string path) => true;

    /// <inheritdoc />
    public byte[]? Key(bool create) => null;
}
