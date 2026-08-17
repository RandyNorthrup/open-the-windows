using System.Globalization;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>
/// In-memory journal store that keeps the live record objects and computes the
/// real hash chain with <see cref="JournalSerializer"/>, so hash-chain and
/// write-before-change ordering can be asserted without touching disk.
/// </summary>
public sealed class FakeJournalStore : IJournalStore
{
    private readonly List<JournalRecord> _records = [];

    /// <summary>When set, <see cref="Begin"/> and <see cref="Update"/> append here for ordering assertions.</summary>
    public IList<string>? Trace { get; init; }

    /// <summary>The records begun, oldest first (live references, so they reflect final state).</summary>
    public IReadOnlyList<JournalRecord> Records => _records;

    /// <summary>How many times <see cref="Update"/> was called.</summary>
    public int UpdateCount { get; private set; }

    /// <inheritdoc />
    public void Begin(JournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        record.PreviousHash = _records.Count > 0 ? _records[^1].Hash : null;
        record.Hash = JournalSerializer.ComputeHash(record);
        _records.Add(record);
        Trace?.Add(string.Create(CultureInfo.InvariantCulture, $"journal:begin:{record.RunId}"));
    }

    /// <inheritdoc />
    public void Update(JournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        record.Hash = JournalSerializer.ComputeHash(record);
        UpdateCount++;
        Trace?.Add("journal:update");
    }

    /// <inheritdoc />
    public IReadOnlyList<JournalSummary> List()
        => [.. Enumerable.Reverse(_records)
            .Select(r => new JournalSummary(r.RunId, r.StartedAt, r.Profile, r.Result, r.Entries.Count, r.RevertOf))];

    /// <inheritdoc />
    public JournalRecord Load(Guid runId)
        => _records.Find(r => r.RunId == runId)
            ?? throw new KeyNotFoundException(string.Create(CultureInfo.InvariantCulture, $"No journal for run {runId}."));
}
