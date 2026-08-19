namespace OpenTheWindows.Core.Audit;

/// <summary>
/// Computes the 0–100 baseline score. Each rule is weighted by its severity; the
/// score is the weighted fraction of <em>scoreable</em> rules (those with a
/// Pass or Fail outcome) that pass. NotApplicable, Manual and Unknown rules are
/// reported but excluded from the score because they are not automatically
/// determinable as pass or fail. A baseline with no scoreable rules scores 100
/// (there is nothing that could fail).
/// </summary>
public static class AuditScoring
{
    /// <summary>The severity weight used in the score (higher = more critical).</summary>
    public static int Weight(BaselineSeverity severity)
        => severity switch
        {
            BaselineSeverity.CatI => 10,
            BaselineSeverity.CatII => 5,
            BaselineSeverity.CatIII => 2,
            BaselineSeverity.L1 => 5,
            BaselineSeverity.L2 => 3,
            BaselineSeverity.Bl => 5,
            BaselineSeverity.Ng => 3,
            BaselineSeverity.Baseline => 5,
            _ => 1,
        };

    /// <summary>The weighted 0–100 score for a set of rule results.</summary>
    public static int Score(IReadOnlyList<AuditRuleResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        int passWeight = 0;
        int scoreableWeight = 0;
        foreach (AuditRuleResult result in results)
        {
            if (result.Outcome is not (AuditOutcome.Pass or AuditOutcome.Fail))
            {
                continue;
            }

            int weight = Weight(result.Severity);
            scoreableWeight += weight;
            if (result.Outcome == AuditOutcome.Pass)
            {
                passWeight += weight;
            }
        }

        return scoreableWeight == 0
            ? 100
            : (int)Math.Round(100.0 * passWeight / scoreableWeight, MidpointRounding.AwayFromZero);
    }
}
