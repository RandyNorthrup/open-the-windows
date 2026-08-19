using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Audit;

/// <summary>The evaluated result of one baseline rule against the live machine.</summary>
/// <param name="RuleId">The framework control id (also the SARIF rule id).</param>
/// <param name="Title">The paraphrased control description.</param>
/// <param name="Severity">The framework severity / profile level.</param>
/// <param name="Outcome">Pass / Fail / NotApplicable / Manual / Unknown.</param>
/// <param name="TweakIds">The catalogue tweak ids the rule maps to (empty for check-only or manual rules).</param>
/// <param name="Evidence">The actual observed value(s) that produced the outcome.</param>
public sealed record AuditRuleResult(
    string RuleId,
    string Title,
    BaselineSeverity Severity,
    AuditOutcome Outcome,
    IReadOnlyList<TweakId> TweakIds,
    string Evidence);
