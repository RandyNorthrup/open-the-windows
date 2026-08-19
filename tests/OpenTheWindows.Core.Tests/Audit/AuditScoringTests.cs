using OpenTheWindows.Core.Audit;

namespace OpenTheWindows.Core.Tests.Audit;

public sealed class AuditScoringTests
{
    private static AuditRuleResult Result(BaselineSeverity severity, AuditOutcome outcome)
        => new("R", "title", severity, outcome, [], "evidence");

    [Fact]
    public void All_pass_scores_100()
    {
        int score = AuditScoring.Score([Result(BaselineSeverity.CatI, AuditOutcome.Pass), Result(BaselineSeverity.L2, AuditOutcome.Pass)]);

        Assert.Equal(100, score);
    }

    [Fact]
    public void All_fail_scores_0()
    {
        int score = AuditScoring.Score([Result(BaselineSeverity.CatI, AuditOutcome.Fail), Result(BaselineSeverity.CatII, AuditOutcome.Fail)]);

        Assert.Equal(0, score);
    }

    [Fact]
    public void The_score_is_weighted_by_severity()
    {
        // CAT I pass (weight 10) + CAT III fail (weight 2) => 10 / 12 => 83.
        int score = AuditScoring.Score([Result(BaselineSeverity.CatI, AuditOutcome.Pass), Result(BaselineSeverity.CatIII, AuditOutcome.Fail)]);

        Assert.Equal(83, score);
    }

    [Fact]
    public void Not_applicable_manual_and_unknown_are_excluded_from_the_score()
    {
        // Only the CAT I pass and CAT II fail are scoreable => 10 / 15 => 67.
        int score = AuditScoring.Score(
        [
            Result(BaselineSeverity.CatI, AuditOutcome.Pass),
            Result(BaselineSeverity.CatII, AuditOutcome.Fail),
            Result(BaselineSeverity.CatI, AuditOutcome.NotApplicable),
            Result(BaselineSeverity.CatI, AuditOutcome.Manual),
            Result(BaselineSeverity.CatI, AuditOutcome.Unknown),
        ]);

        Assert.Equal(67, score);
    }

    [Fact]
    public void No_scoreable_rules_scores_100()
    {
        int score = AuditScoring.Score(
        [
            Result(BaselineSeverity.CatI, AuditOutcome.NotApplicable),
            Result(BaselineSeverity.CatII, AuditOutcome.Manual),
            Result(BaselineSeverity.CatIII, AuditOutcome.Unknown),
        ]);

        Assert.Equal(100, score);
    }

    [Fact]
    public void The_summary_counts_each_outcome()
    {
        var summary = AuditSummary.From(
        [
            Result(BaselineSeverity.CatI, AuditOutcome.Pass),
            Result(BaselineSeverity.CatI, AuditOutcome.Fail),
            Result(BaselineSeverity.CatI, AuditOutcome.Fail),
            Result(BaselineSeverity.CatI, AuditOutcome.NotApplicable),
            Result(BaselineSeverity.CatI, AuditOutcome.Manual),
            Result(BaselineSeverity.CatI, AuditOutcome.Unknown),
        ]);

        Assert.Equal(6, summary.Total);
        Assert.Equal(1, summary.Pass);
        Assert.Equal(2, summary.Fail);
        Assert.Equal(1, summary.NotApplicable);
        Assert.Equal(1, summary.Manual);
        Assert.Equal(1, summary.Unknown);
    }
}
