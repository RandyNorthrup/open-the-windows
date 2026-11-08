namespace OpenTheWindows.Core.Profiles;

/// <summary>
/// The public keys a machine trusts to sign profiles and catalogue files. The
/// Windows implementation reads only keys held in an administrator-owned folder and
/// owned by an administrator, so a standard user cannot add a signer.
/// </summary>
public interface ITrustedKeySource
{
    /// <summary>Where the keys come from, for messages.</summary>
    string Description { get; }

    /// <summary>The PEM text of every trusted public key (unreadable or untrusted files are skipped).</summary>
    IReadOnlyList<string> ReadPublicKeysPem();
}
