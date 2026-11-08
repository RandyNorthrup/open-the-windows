using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Startup;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>Scriptable <see cref="IStartupSource"/>: machine-wide entries always, per-user entries only when a user is signed in.</summary>
public sealed class FakeStartupSource : IStartupSource
{
    /// <summary>The entries listed (per-user locations are listed only when a SID is passed).</summary>
    public IList<StartupEntry> Entries { get; } = [];

    /// <inheritdoc />
    public IReadOnlyList<StartupEntry> List(string? userSid)
        => [.. Entries.Where(e => userSid is not null || e.Location is not (StartupLocation.UserRun or StartupLocation.UserStartupFolder))];
}
