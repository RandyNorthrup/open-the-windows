using OpenTheWindows.Core.Releases;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>Scriptable <see cref="IAppReleaseChecker"/> for CLI and GUI tests.</summary>
public sealed class FakeAppReleaseChecker : IAppReleaseChecker
{
    /// <summary>What <see cref="CheckAsync"/> returns.</summary>
    public AppReleaseCheckResult Result { get; set; } = new(true, false, "0.1.0", "0.1.0", null, "Up to date.");

    /// <summary>How many times the checker ran.</summary>
    public int CallCount { get; private set; }

    /// <inheritdoc />
    public Task<AppReleaseCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(Result);
    }
}
