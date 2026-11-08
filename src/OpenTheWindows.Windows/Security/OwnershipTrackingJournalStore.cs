using System.Runtime.Versioning;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.Windows.Security;

/// <summary>
/// A journal store that also keeps the <see cref="PolicyOwnershipLedger"/> current: when
/// a run's record is saved with a final state, the machine-policy values it wrote (or,
/// for a revert, un-wrote) are folded into the ledger. Ledger trouble never fails a run —
/// the journal is the record of truth; the ledger only refines managed detection.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class OwnershipTrackingJournalStore : IJournalStore
{
    private readonly IJournalStore _inner;
    private readonly PolicyOwnershipLedger _ledger;

    /// <summary>Wraps <paramref name="inner"/>, recording finished runs into <paramref name="ledger"/>.</summary>
    public OwnershipTrackingJournalStore(IJournalStore inner, PolicyOwnershipLedger ledger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
    }

    /// <inheritdoc />
    public void Begin(JournalRecord record) => _inner.Begin(record);

    /// <inheritdoc />
    public void Update(JournalRecord record)
    {
        _inner.Update(record);
        try
        {
            _ledger.Record(record);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The run's outcome is already journaled; a stale ledger only makes detection stricter.
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<JournalSummary> List() => _inner.List();

    /// <inheritdoc />
    public JournalRecord Load(Guid runId) => _inner.Load(runId);
}
