namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>A <see cref="TimeProvider"/> that always returns a fixed instant, for deterministic report timestamps.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => now;
}
