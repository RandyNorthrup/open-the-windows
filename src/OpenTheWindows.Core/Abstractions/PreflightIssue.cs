namespace OpenTheWindows.Core.Abstractions;

/// <summary>One pre-flight finding.</summary>
/// <param name="Check">Which condition was evaluated.</param>
/// <param name="Blocking">Whether this issue prevents the run from proceeding.</param>
/// <param name="Detail">Human-readable explanation.</param>
public sealed record PreflightIssue(PreflightCheck Check, bool Blocking, string Detail);
