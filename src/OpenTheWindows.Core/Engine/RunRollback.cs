using System.Globalization;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.Core.Engine;

/// <summary>
/// Undoes a run from its journal, in reverse: every action that was applied, every
/// action that failed part-way (its prior was captured before the write, and
/// restoring is idempotent), and — for a run a crash interrupted — the action that
/// was in flight. An action whose prior cannot be put back is reported
/// <see cref="ApplyState.NotReversible"/> rather than claimed undone.
/// </summary>
internal sealed class RunRollback
{
    /// <summary>Entry outcomes in precedence order: the first one any action has is the entry's outcome.</summary>
    private static readonly ApplyState[] AggregateOrder =
    [
        ApplyState.Failed, ApplyState.NotReversible, ApplyState.Applied, ApplyState.RolledBack,
        ApplyState.Pending, ApplyState.Skipped,
    ];

    private readonly ActionApplier _applier;
    private readonly RunJournal _journal;

    public RunRollback(ActionApplier applier, RunJournal journal)
    {
        _applier = applier;
        _journal = journal;
    }

    /// <summary>
    /// Rolls <paramref name="record"/> back. <paramref name="failedEntry"/>, when set, is
    /// the entry whose failure triggered the rollback and stays reported as failed.
    /// <paramref name="inFlight"/>, when set, is a pending action that may have been
    /// part-written when the process stopped.
    /// </summary>
    public RunState RollBack(JournalRecord record, Guid runId, JournalEntry? failedEntry, JournalAction? inFlight)
    {
        bool clean = true;
        foreach (JournalEntry entry in Enumerable.Reverse(record.Entries))
        {
            foreach (JournalAction action in Enumerable.Reverse(entry.Actions).Where(a => NeedsRestore(a, inFlight)))
            {
                clean &= RestoreOne(action, runId, entry.Id);
                _journal.TryUpdate(record);
            }

            entry.Result = ReferenceEquals(entry, failedEntry) ? ApplyState.Failed : Aggregate(entry);
        }

        _journal.Audit(AuditEventId.RunRolledBack, "Run rolled back after a failure.", runId, null);
        return clean ? RunState.RolledBack : RunState.Failed;
    }

    /// <summary>
    /// Finds runs a crash or power loss left <see cref="RunState.InProgress"/> and
    /// settles each one. The caller holds the machine run lock, so no such journal
    /// can belong to a live run. A run whose every action finished is marked
    /// completed; any other is rolled back to its pre-run state.
    /// </summary>
    public IReadOnlyList<Guid> RecoverInterrupted()
    {
        List<Guid> recovered = [];
        foreach (JournalSummary summary in _journal.Store.List()
                     .Where(s => s.Result == RunState.InProgress && s.Integrity == JournalIntegrity.Verified))
        {
            JournalRecord record = _journal.Store.Load(summary.RunId);
            List<JournalAction> actions = [.. record.Entries.SelectMany(e => e.Actions)];
            JournalAction? inFlight = actions.Find(a => a.Result == ApplyState.Pending);
            if (inFlight is null && actions.TrueForAll(a => a.Result != ApplyState.Failed))
            {
                record.Result = RunState.Completed;
            }
            else
            {
                record.Result = RollBack(record, record.RunId, failedEntry: null, inFlight);
            }

            _journal.TryUpdate(record);
            _journal.Audit(AuditEventId.Rollback,
                string.Create(CultureInfo.InvariantCulture, $"Recovered run {record.RunId} interrupted before it finished ({record.Result})."),
                record.RunId, null);
            recovered.Add(record.RunId);
        }

        return recovered;
    }

    /// <summary>The entry outcome derived from its actions' outcomes.</summary>
    public static ApplyState Aggregate(JournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return AggregateOrder
            .Where(state => entry.Actions.Any(a => a.Result == state))
            .DefaultIfEmpty(ApplyState.NotApplicable)
            .First();
    }

    private static bool NeedsRestore(JournalAction action, JournalAction? inFlight)
        => action.Result is ApplyState.Applied or ApplyState.Failed || ReferenceEquals(action, inFlight);

    private bool RestoreOne(JournalAction action, Guid runId, string entryId)
    {
        bool wasApplied = action.Result == ApplyState.Applied;
        if (!ActionApplier.CanRestore(action.Action, action.Prior))
        {
            // A failed or in-flight write with no restorable prior most likely changed nothing.
            if (wasApplied)
            {
                // Do what can be done (a deprovisioned app still staged is reprovisioned), but report it honestly.
                ApplyEngine.Capture(() => _applier.Restore(action.Action, action.Prior, action.Sid));
                action.Result = ApplyState.NotReversible;
                _journal.Audit(AuditEventId.Error,
                    string.Create(CultureInfo.InvariantCulture, $"Cannot restore {action.Target}: its prior state cannot be put back."), runId, entryId);
            }

            return !wasApplied;
        }

        Exception? failure = ApplyEngine.Capture(() => _applier.Restore(action.Action, action.Prior, action.Sid));
        if (failure is null)
        {
            action.Result = ApplyState.RolledBack;
            _journal.Audit(AuditEventId.Rollback, string.Create(CultureInfo.InvariantCulture, $"Rolled back {action.Target}."), runId, entryId);
            return true;
        }

        action.Result = ApplyState.Failed;
        _journal.Audit(AuditEventId.Error,
            string.Create(CultureInfo.InvariantCulture, $"Rollback failed for {action.Target}: {failure.Message}"), runId, entryId);
        return false;
    }
}
