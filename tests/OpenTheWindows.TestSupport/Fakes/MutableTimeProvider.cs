namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>
/// A <see cref="TimeProvider"/> whose current instant can be advanced, for tests
/// that need two operations to carry distinct, strictly increasing timestamps
/// (for example two apply runs whose journal ordering must be unambiguous).
/// </summary>
public sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>Moves the clock forward by <paramref name="delta"/> (must be non-negative).</summary>
    public void Advance(TimeSpan delta)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delta, TimeSpan.Zero);
        _now += delta;
    }
}
