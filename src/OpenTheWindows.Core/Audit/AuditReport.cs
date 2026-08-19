using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Core.Audit;

/// <summary>
/// The read-only result of auditing the machine against one <see cref="Baseline"/>:
/// a 0–100 score weighted by severity, per-rule outcomes with the actual observed
/// evidence, and the OS facts at the time of the run. Produced by
/// <see cref="AuditEngine"/>; never changes machine state.
/// </summary>
/// <param name="BaselineId">The baseline's id.</param>
/// <param name="BaselineTitle">The baseline's human title.</param>
/// <param name="At">When the audit ran (from the scan clock).</param>
/// <param name="Os">The operating-system facts at the time of the run.</param>
/// <param name="Score">Weighted 0–100 score (see <see cref="AuditScoring"/>).</param>
/// <param name="Summary">Outcome counts.</param>
/// <param name="Results">Per-rule results in baseline order.</param>
public sealed record AuditReport(
    string BaselineId,
    string BaselineTitle,
    DateTimeOffset At,
    OperatingSystemFacts Os,
    int Score,
    AuditSummary Summary,
    IReadOnlyList<AuditRuleResult> Results);
