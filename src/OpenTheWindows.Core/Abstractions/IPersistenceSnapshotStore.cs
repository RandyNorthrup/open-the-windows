using OpenTheWindows.Core.Persistence;

namespace OpenTheWindows.Core.Abstractions;

/// <summary>Keeps the persistence-watch baseline.</summary>
public interface IPersistenceSnapshotStore
{
    /// <summary>The saved baseline, or <see langword="null"/> when none has been taken (or it cannot be trusted).</summary>
    PersistenceSnapshot? Load();

    /// <summary>Replaces the baseline with <paramref name="snapshot"/>.</summary>
    void Save(PersistenceSnapshot snapshot);
}
