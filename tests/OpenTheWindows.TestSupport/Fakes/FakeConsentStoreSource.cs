using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Privacy;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>Scriptable <see cref="IConsentStoreSource"/>: machine-wide records always, per-user ones only when a user is signed in.</summary>
public sealed class FakeConsentStoreSource : IConsentStoreSource
{
    /// <summary>The records listed.</summary>
    public IList<ConsentStoreRecord> Records { get; } = [];

    /// <inheritdoc />
    public IReadOnlyList<ConsentStoreRecord> List(string? userSid) => [.. Records.Where(r => userSid is not null || !r.PerUser)];
}
