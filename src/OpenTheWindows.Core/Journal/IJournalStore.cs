namespace OpenTheWindows.Core.Journal;

/// <summary>
/// Persists journals. <see cref="Begin"/> writes the initial record (with every
/// prior state) before any machine change and stamps its hash chain;
/// <see cref="Update"/> re-flushes the record after each action; <see cref="List"/>
/// and <see cref="Load"/> read them back for <c>history</c> and <c>revert</c>.
/// </summary>
public interface IJournalStore
{
    /// <summary>Persists <paramref name="record"/> for the first time, setting its <c>PreviousHash</c> and <c>Hash</c>.</summary>
    void Begin(JournalRecord record);

    /// <summary>Re-persists <paramref name="record"/> with its current state and a recomputed hash.</summary>
    void Update(JournalRecord record);

    /// <summary>Lists every journal, newest first.</summary>
    IReadOnlyList<JournalSummary> List();

    /// <summary>Loads the full record for <paramref name="runId"/>.</summary>
    JournalRecord Load(Guid runId);
}
