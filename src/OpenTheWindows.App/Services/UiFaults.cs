namespace OpenTheWindows.App.Services;

/// <summary>
/// The one rule for which faults a page shows the user rather than let the process die:
/// every fault except the ones that leave the runtime unable to continue. Operational
/// failures are already journaled and rolled back by the engine; this only decides how
/// an unexpected one is reported.
/// </summary>
internal static class UiFaults
{
    /// <summary>Whether <paramref name="error"/> should be reported in the page instead of propagating.</summary>
    public static bool IsReportable(Exception error)
        => error is not (OutOfMemoryException or StackOverflowException or InsufficientExecutionStackException);
}
