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

    /// <summary>Runs whose journal fails verification when loaded (as a file changed after it was listed would).</summary>
    public ISet<Guid> FailVerification { get; } = new HashSet<Guid>();

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
        UpdateCount++;
        if (UpdateCount == FailUpdateNumber)
        {
            throw new IOException(string.Create(CultureInfo.InvariantCulture, $"Injected journal failure on update {UpdateCount}."));
        }

        record.Hash = JournalSerializer.ComputeHash(record);
        Trace?.Add("journal:update");
    }

    /// <summary>When set, the <see cref="Update"/> call with this 1-based number throws (a full disk or a sharing violation).</summary>
    public int? FailUpdateNumber { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<JournalSummary> List()
        => [.. Enumerable.Reverse(_records)
            .Select(r => new JournalSummary(r.RunId, r.StartedAt, r.Profile, r.Result, r.Entries.Count, r.RevertOf))];

    /// <inheritdoc />
    public JournalRecord Load(Guid runId)
        => FailVerification.Contains(runId)
            ? throw new JournalIntegrityException(string.Create(CultureInfo.InvariantCulture, $"The journal for run {runId} does not verify."))
            : _records.Find(r => r.RunId == runId)
                ?? throw new KeyNotFoundException(string.Create(CultureInfo.InvariantCulture, $"No journal for run {runId}."));
}
