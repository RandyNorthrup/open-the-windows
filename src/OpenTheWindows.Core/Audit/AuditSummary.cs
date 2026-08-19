namespace OpenTheWindows.Core.Audit;

/// <summary>Counts of rule outcomes in an <see cref="AuditReport"/>.</summary>
/// <param name="Total">All rules.</param>
/// <param name="Pass">Rules satisfied.</param>
/// <param name="Fail">Rules not satisfied.</param>
/// <param name="NotApplicable">Rules that do not apply to this machine.</param>
/// <param name="Manual">Rules needing a human decision (policy-managed or manual-only).</param>
/// <param name="Unknown">Rules that could not be determined.</param>
public sealed record AuditSummary(
    int Total,
    int Pass,
    int Fail,
    int NotApplicable,
    int Manual,
    int Unknown)
{
    /// <summary>Builds the summary by counting outcomes.</summary>
    public static AuditSummary From(IReadOnlyList<AuditRuleResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return new AuditSummary(
            results.Count,
            results.Count(r => r.Outcome == AuditOutcome.Pass),
            results.Count(r => r.Outcome == AuditOutcome.Fail),
            results.Count(r => r.Outcome == AuditOutcome.NotApplicable),
            results.Count(r => r.Outcome == AuditOutcome.Manual),
            results.Count(r => r.Outcome == AuditOutcome.Unknown));
    }
}
