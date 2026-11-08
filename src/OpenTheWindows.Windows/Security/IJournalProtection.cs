namespace OpenTheWindows.Windows.Security;

/// <summary>
/// How a journal store protects its folder and records: who may write the folder,
/// which files are trusted, and the key (if any) that MACs each record. The machine
/// store uses <see cref="MachineJournalProtection"/>; a per-user store (the unelevated
/// logon fallback writes only the caller's own hive) and tests use
/// <see cref="NoJournalProtection"/>.
/// </summary>
internal interface IJournalProtection
{
    /// <summary>Ensures the folder may be written by this (privileged) process; throws when it cannot be made safe.</summary>
    void PrepareForWrite(string directory);

    /// <summary>Whether records in the folder can be trusted at all.</summary>
    bool IsTrustworthy(string directory);

    /// <summary>Whether an individual journal file is trusted (owner and permissions).</summary>
    bool IsTrustedFile(string path);

    /// <summary>
    /// The MAC key, creating it when <paramref name="create"/> is set; <see langword="null"/>
    /// when records are not keyed or this process cannot read the key.
    /// </summary>
    byte[]? Key(bool create);
}
