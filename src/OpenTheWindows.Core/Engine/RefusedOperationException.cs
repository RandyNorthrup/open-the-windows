namespace OpenTheWindows.Core.Engine;

/// <summary>
/// Thrown when a code-level safety rule refuses an action regardless of what the
/// catalogue asked for: reconfiguring a protected or protected-process service,
/// or running an executable that is not on the allow-list. The engine catches it
/// at plan time so a refused entry is never partially applied, and it is the
/// last-line guard if a writer is called directly.
/// </summary>
public sealed class RefusedOperationException : Exception
{
    /// <summary>Creates the exception with a message describing the refused operation.</summary>
    public RefusedOperationException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and an inner cause.</summary>
    public RefusedOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception with no message.</summary>
    public RefusedOperationException()
    {
    }
}
