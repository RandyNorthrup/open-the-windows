using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Persistence;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>In-memory <see cref="IPersistenceSnapshotStore"/>.</summary>
public sealed class FakePersistenceSnapshotStore : IPersistenceSnapshotStore
{
    /// <summary>The saved baseline.</summary>
    public PersistenceSnapshot? Saved { get; set; }

    /// <inheritdoc />
    public PersistenceSnapshot? Load() => Saved;

    /// <inheritdoc />
    public void Save(PersistenceSnapshot snapshot) => Saved = snapshot;
}
