namespace OpenTheWindows.Cli;

/// <summary>The restore-point outcome in <see cref="ApplyJsonReport"/>.</summary>
/// <param name="Status">Created / Skipped24h / Disabled / Failed / NotRequested.</param>
/// <param name="SequenceNumber">The restore point sequence number when one was created.</param>
internal sealed record ApplyJsonRestore(string Status, long? SequenceNumber);
