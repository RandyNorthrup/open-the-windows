namespace OpenTheWindows.Core.Journal;

/// <summary>The System Restore point outcome for a run, recorded truthfully.</summary>
/// <param name="Requested">Whether a restore point was asked for.</param>
/// <param name="Created">Whether one was actually created and verified.</param>
/// <param name="Reason">Why it was or was not created (the status name, e.g. "Created", "Skipped24h", "Disabled").</param>
/// <param name="SequenceNumber">The restore point sequence number when created; otherwise <see langword="null"/>.</param>
public sealed record JournalRestorePoint(bool Requested, bool Created, string Reason, long? SequenceNumber);
