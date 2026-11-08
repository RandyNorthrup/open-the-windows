using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>
/// In-process run lock: acquiring succeeds unless <see cref="HeldElsewhere"/> is set
/// (simulating another process mid-run), and it records how often it was taken.
/// </summary>
public sealed class FakeRunLock : IRunLock
{
    /// <summary>When set, every acquire fails as if another run held the machine lock.</summary>
    public bool HeldElsewhere { get; set; }

    /// <summary>How many times the lock was successfully taken.</summary>
    public int Acquisitions { get; private set; }

    /// <summary>Whether the lock is currently held by this process.</summary>
    public bool IsHeld { get; private set; }

    /// <inheritdoc />
    public IDisposable? TryAcquire(TimeSpan timeout)
    {
        if (HeldElsewhere || IsHeld)
        {
            return null;
        }

        IsHeld = true;
        Acquisitions++;
        return new Release(this);
    }

    private sealed class Release(FakeRunLock owner) : IDisposable
    {
        public void Dispose() => owner.IsHeld = false;
    }
}
