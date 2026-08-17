namespace OpenTheWindows.Core.Abstractions;

/// <summary>One audit record, written to both the Windows Event Log and the JSONL audit trail.</summary>
/// <param name="Id">The stable event identifier.</param>
/// <param name="At">When the event occurred (from <see cref="TimeProvider"/>).</param>
/// <param name="Message">Human-readable message; must never contain secrets.</param>
/// <param name="RunId">The apply/revert run the event belongs to, when applicable.</param>
/// <param name="EntryId">The catalogue entry the event concerns, when applicable.</param>
public sealed record AuditEvent(AuditEventId Id, DateTimeOffset At, string Message, Guid? RunId, string? EntryId);
