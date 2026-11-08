namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Serialises apply, revert and crash recovery across every process on the
/// machine (the GUI, the CLI and the SYSTEM remediation task), so two runs never
/// interleave writes or journal updates, and a journal still marked in progress
/// while the lock is held can only belong to an interrupted run.
/// </summary>
public interface IRunLock
{
    /// <summary>
    /// Takes the lock, waiting up to <paramref name="timeout"/>. Returns a handle
    /// that releases it when disposed, or <see langword="null"/> when another run
    /// still holds it after the wait.
    /// </summary>
    IDisposable? TryAcquire(TimeSpan timeout);
}
