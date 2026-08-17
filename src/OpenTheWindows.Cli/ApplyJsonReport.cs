namespace OpenTheWindows.Cli;

/// <summary>Stable JSON shape for <c>otw apply --json</c> / <c>otw revert --json</c>.</summary>
/// <param name="RunId">The journaled run id (empty for a what-if or rejected plan).</param>
/// <param name="State">The run's overall state.</param>
/// <param name="Requires">The aggregate restart requirement.</param>
/// <param name="RestorePoint">The restore-point outcome.</param>
/// <param name="Errors">Blocking plan errors, if any.</param>
/// <param name="Entries">Per-entry outcomes.</param>
internal sealed record ApplyJsonReport(
    string RunId,
    string State,
    string Requires,
    ApplyJsonRestore RestorePoint,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ApplyJsonEntry> Entries);
