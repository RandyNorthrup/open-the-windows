using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Persistence;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>Scriptable <see cref="IPersistenceSource"/>: returns a copy of <see cref="Items"/> as they are when captured.</summary>
public sealed class FakePersistenceSource : IPersistenceSource
{
    /// <summary>The autostarts present now.</summary>
    public IList<PersistenceItem> Items { get; } = [];

    /// <inheritdoc />
    public IReadOnlyList<PersistenceItem> Capture(string? userSid) => [.. Items];
}
