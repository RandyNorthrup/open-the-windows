namespace OpenTheWindows.Core.Abstractions;

/// <summary>The result of a System Restore point request, reported truthfully in the journal.</summary>
/// <param name="Status">What actually happened.</param>
/// <param name="SequenceNumber">The restore point sequence number when one was created; otherwise <see langword="null"/>.</param>
/// <param name="Detail">Human-readable explanation (the cause when not created).</param>
public sealed record RestorePointResult(RestorePointStatus Status, long? SequenceNumber, string Detail);
