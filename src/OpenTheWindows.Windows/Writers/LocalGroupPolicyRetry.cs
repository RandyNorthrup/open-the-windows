namespace OpenTheWindows.Windows.Writers;

/// <summary>
/// Bounded retry for a Local Group Policy operation that loses a race for the
/// <c>Registry.pol</c> lock. When a second elevated writer touches the same policy
/// file at the same moment — a concurrent <c>gpupdate</c>, the registry policy
/// client-side extension, or another Open the Windows invocation — the Group
/// Policy COM save surfaces <c>ERROR_SHARING_VIOLATION</c> (<c>0x80070020</c>).
/// The contention is brief, so a short backed-off retry succeeds where a single
/// attempt would fail the whole apply and force a rollback. A non-sharing failure,
/// or exhaustion of the attempts, is rethrown unchanged so real errors still
/// surface.
/// </summary>
internal static class LocalGroupPolicyRetry
{
    /// <summary><c>HRESULT_FROM_WIN32(ERROR_SHARING_VIOLATION)</c>.</summary>
    internal const int SharingViolationHResult = unchecked((int)0x80070020);

    /// <summary>Total attempts before the sharing violation is allowed to propagate.</summary>
    internal const int MaxAttempts = 5;

    /// <summary>Wait between attempts.</summary>
    internal static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Runs <paramref name="operation"/>, retrying when it fails with a
    /// <c>Registry.pol</c> sharing violation. <paramref name="sleep"/> performs the
    /// wait between attempts (injected so callers control the clock and tests need
    /// not block).
    /// </summary>
    internal static void Run(Action operation, Action<TimeSpan> sleep)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(sleep);

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                operation();
                return;
            }
            catch (InvalidOperationException ex) when (attempt < MaxAttempts && IsSharingViolation(ex))
            {
                sleep(RetryDelay);
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="exception"/> — or the inner cause a failed COM save is
    /// wrapped in — is a <c>Registry.pol</c> sharing violation.
    /// </summary>
    internal static bool IsSharingViolation(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.HResult == SharingViolationHResult
            || (exception.InnerException is { } inner && inner.HResult == SharingViolationHResult);
    }
}
