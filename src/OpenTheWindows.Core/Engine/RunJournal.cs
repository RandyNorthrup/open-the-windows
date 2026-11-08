using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.Core.Engine;

/// <summary>
/// The journal and audit side of a run. A failed journal update is returned to the
/// caller (the journal is the recovery record, so the run must react: roll back),
/// while an audit write is best-effort — a full disk or an unavailable Event Log
/// must never abort a run half-way and leave the machine part-changed.
/// </summary>
internal sealed class RunJournal
{
    private readonly ApplyServices _services;
    private readonly TimeProvider _time;

    public RunJournal(ApplyServices services, TimeProvider time)
    {
        _services = services;
        _time = time;
    }

    /// <summary>The journal store.</summary>
    public IJournalStore Store => _services.Journal;

    /// <summary>Re-persists <paramref name="record"/>; returns the failure, or <see langword="null"/> on success.</summary>
    public Exception? TryUpdate(JournalRecord record) => ApplyEngine.Capture(() => _services.Journal.Update(record));

    /// <summary>Writes an audit event; a sink failure is swallowed so it cannot interrupt a run.</summary>
    public void Audit(AuditEventId id, string message, Guid? runId, string? entryId)
        => ApplyEngine.Capture(() => _services.Audit.Write(new AuditEvent(id, _time.GetUtcNow(), message, runId, entryId)));
}
