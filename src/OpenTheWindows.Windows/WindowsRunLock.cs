using System.Runtime.Versioning;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows;

/// <summary>
/// Machine-wide run lock over a named mutex. The default instance lives in the
/// <c>Global\</c> namespace so the GUI, the CLI and the SYSTEM remediation task all
/// contend for the same lock (creating a <c>Global\</c> object needs
/// <c>SeCreateGlobalPrivilege</c>, which standard users lack, so they cannot squat
/// it). A per-user, unelevated run uses a <c>Local\</c> (session) lock instead. A
/// mutex abandoned by a crashed run is taken over; crash recovery then settles that
/// run's journal.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsRunLock : IRunLock
{
    /// <summary>The machine-wide lock name.</summary>
    public const string MachineLockName = @"Global\OpenTheWindows.Run";

    /// <summary>The per-session lock name used by unelevated per-user runs.</summary>
    public const string SessionLockName = @"Local\OpenTheWindows.UserRun";

    private readonly string _name;

    /// <summary>Uses the machine-wide lock.</summary>
    public WindowsRunLock()
        : this(MachineLockName)
    {
    }

    /// <summary>Uses the lock called <paramref name="name"/>.</summary>
    public WindowsRunLock(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
    }

    /// <inheritdoc />
    public IDisposable? TryAcquire(TimeSpan timeout)
    {
        HeldLock? held = new(_name);
        try
        {
            if (!held.TryEnter(timeout))
            {
                return null;
            }

            HeldLock acquired = held;
            held = null;
            return acquired;
        }
        finally
        {
            held?.Dispose();
        }
    }

    /// <summary>Owns one handle to the named mutex and releases it on dispose when it was entered.</summary>
    private sealed class HeldLock : IDisposable
    {
        private readonly Mutex _mutex;
        private bool _entered;
        private bool _disposed;

        public HeldLock(string name) => _mutex = new Mutex(initiallyOwned: false, name);

        public bool TryEnter(TimeSpan timeout)
        {
            try
            {
                _entered = _mutex.WaitOne(timeout);
            }
            catch (AbandonedMutexException)
            {
                // The previous holder exited without releasing: this thread now owns the mutex.
                _entered = true;
            }

            return _entered;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_entered)
            {
                _mutex.ReleaseMutex();
            }

            _mutex.Dispose();
        }
    }
}
