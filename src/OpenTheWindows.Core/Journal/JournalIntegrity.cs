namespace OpenTheWindows.Core.Journal;

/// <summary>Whether a journal on disk can be trusted for revert and recovery.</summary>
public enum JournalIntegrity
{
    /// <summary>The record's hash or machine-keyed MAC verifies, and the file is owned by an administrator or SYSTEM.</summary>
    Verified = 0,

    /// <summary>
    /// The record parsed, but its machine-keyed MAC could not be checked because this
    /// process cannot read the key (an unelevated history listing). Revert, which runs
    /// elevated, always verifies.
    /// </summary>
    Unverified = 1,

    /// <summary>The record parsed but its hash/MAC or file owner does not check out; it is never replayed.</summary>
    Tampered = 2,

    /// <summary>The file could not be read or parsed (for example a write torn by a crash); it is never replayed.</summary>
    Unreadable = 3,
}
