namespace OpenTheWindows.Core.Journal;

/// <summary>
/// Thrown when a journal cannot be trusted: its hash or machine-keyed MAC does not
/// verify, its file is not owned by an administrator or SYSTEM, or it cannot be
/// parsed. Revert and recovery refuse to replay such a journal.
/// </summary>
public sealed class JournalIntegrityException : Exception
{
    /// <summary>Creates the exception with no message.</summary>
    public JournalIntegrityException()
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/>.</summary>
    public JournalIntegrityException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with <paramref name="message"/> and the <paramref name="innerException"/> that caused it.</summary>
    public JournalIntegrityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
