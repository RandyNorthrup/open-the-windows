using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Audit;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Model;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Core.Tests.Audit;

public sealed class AuditEngineTests
{
    private static readonly OperatingSystemFacts Os = FakeOperatingSystemInfo.Windows11Pro24H2();
    private static readonly DateTimeOffset At = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Uri Source = new("https://example.test/control");

    private static TweakCatalog Catalog() => AuditTestCatalog.LoadBuiltIn();

    private static AuditEngine Engine(FakeReaders readers, IMachineHealthProbe health)
        => new(FakeScanEngine.Create(readers, Os, At), health, Catalog());

    private static HealthCheckResult Hc(string id, HealthStatus status)
        => new(id, id, status, status.ToString(), null);

    private static AuditOutcome Outcome(AuditReport report, string ruleId)
        => report.Results.Single(r => string.Equals(r.RuleId, ruleId, StringComparison.Ordinal)).Outcome;

    private static Baseline CheckBaseline(params (string RuleId, string HealthId, BaselineSeverity Severity)[] rules)
        => new(null, 1, "unit", "Unit Baseline",
            [.. rules.Select(r => new BaselineRule(r.RuleId, "title", r.Severity, [], new BaselineCheck(r.HealthId), [Source]))]);

    [Fact]
    public void Health_statuses_map_to_outcomes_and_the_score_is_weighted()
    {
        var health = new FakeHealthProbe(
        [
            Hc(HealthCheckIds.SecureBoot, HealthStatus.Pass),
            Hc(HealthCheckIds.Tpm, HealthStatus.Fail),
            Hc(HealthCheckIds.TamperProtection, HealthStatus.Warn),
            Hc(HealthCheckIds.JoinState, HealthStatus.NotApplicable),
            Hc(HealthCheckIds.WinRe, HealthStatus.Unknown),
        ]);
        Baseline baseline = CheckBaseline(
            ("SB", HealthCheckIds.SecureBoot, BaselineSeverity.CatI),
            ("TPM", HealthCheckIds.Tpm, BaselineSeverity.CatI),
            ("TP", HealthCheckIds.TamperProtection, BaselineSeverity.CatII),
            ("JS", HealthCheckIds.JoinState, BaselineSeverity.CatIII),
            ("RE", HealthCheckIds.WinRe, BaselineSeverity.CatII));

        AuditReport report = Engine(new FakeReaders(), health).Audit(baseline);

        Assert.Equal(AuditOutcome.Pass, Outcome(report, "SB"));
        Assert.Equal(AuditOutcome.Fail, Outcome(report, "TPM"));
        Assert.Equal(AuditOutcome.Fail, Outcome(report, "TP")); // Warn is treated as Fail for compliance.
        Assert.Equal(AuditOutcome.NotApplicable, Outcome(report, "JS"));
        Assert.Equal(AuditOutcome.Unknown, Outcome(report, "RE"));

        // Scoreable: SB pass (10) + TPM fail (10) + TP fail (5) => 10 / 25 => 40.
        Assert.Equal(40, report.Score);
        Assert.Equal(1, report.Summary.Pass);
        Assert.Equal(2, report.Summary.Fail);
        Assert.Equal(1, report.Summary.NotApplicable);
        Assert.Equal(1, report.Summary.Unknown);
        Assert.Equal(5, report.Summary.Total);
        Assert.Equal("unit", report.BaselineId);
        Assert.Equal(At, report.At);
    }

    [Fact]
    public void A_checkOnly_rule_whose_health_check_did_not_run_is_unknown()
    {
        Baseline baseline = CheckBaseline(("X", HealthCheckIds.SecureBoot, BaselineSeverity.CatI));

        AuditReport report = Engine(new FakeReaders(), new FakeHealthProbe([])).Audit(baseline);

        Assert.Equal(AuditOutcome.Unknown, Outcome(report, "X"));
        Assert.Equal(100, report.Score); // Nothing scoreable.
    }

    [Fact]
    public void The_score_rises_when_a_failing_check_starts_passing()
    {
        Baseline baseline = CheckBaseline(("X", HealthCheckIds.SecureBoot, BaselineSeverity.CatI));

        int before = Engine(new FakeReaders(), new FakeHealthProbe([Hc(HealthCheckIds.SecureBoot, HealthStatus.Fail)])).Audit(baseline).Score;
        int after = Engine(new FakeReaders(), new FakeHealthProbe([Hc(HealthCheckIds.SecureBoot, HealthStatus.Pass)])).Audit(baseline).Score;

        Assert.Equal(0, before);
        Assert.Equal(100, after);
        Assert.True(after > before);
    }

    [Fact]
    public void A_manual_rule_has_no_tweaks_or_check()
    {
        Baseline baseline = new(null, 1, "unit", "Unit Baseline",
            [new BaselineRule("M", "Manual only", BaselineSeverity.CatII, [], null, [Source])]);

        AuditReport report = Engine(new FakeReaders(), new FakeHealthProbe([])).Audit(baseline);

        Assert.Equal(AuditOutcome.Manual, Outcome(report, "M"));
        Assert.Equal(100, report.Score); // Manual is excluded from the score.
        Assert.Equal(1, report.Summary.Manual);
    }

    [Fact]
    public void A_compliant_tweak_passes_and_a_drifting_tweak_fails()
    {
        const string TweakId = "privacy.advertising-id.disable";
        TweakCatalog catalog = Catalog();
        RegistryAction registry = catalog.Get(new TweakId(TweakId)).Actions.OfType<RegistryAction>().First();
        Baseline baseline = new(null, 1, "unit", "Unit Baseline",
            [new BaselineRule("ADV", "Disable the advertising id", BaselineSeverity.CatII, [new TweakId(TweakId)], null, [Source])]);

        var compliant = new FakeReaders
        {
            OnInteractiveUser = () => new InteractiveUser("S-1-5-21-1", "Tester", true),
            OnRegistry = (_, _, path, name) =>
                string.Equals(path, registry.Path, StringComparison.Ordinal) && string.Equals(name, registry.Name, StringComparison.Ordinal)
                    ? new RegistryValueSnapshot(true, registry.Type, registry.Value)
                    : new RegistryValueSnapshot(false, null, null),
        };
        AuditReport pass = Engine(compliant, new FakeHealthProbe([])).Audit(baseline);
        Assert.Equal(AuditOutcome.Pass, Outcome(pass, "ADV"));
        Assert.Equal(100, pass.Score);

        var drift = new FakeReaders { OnInteractiveUser = () => new InteractiveUser("S-1-5-21-1", "Tester", true) };
        AuditReport fail = Engine(drift, new FakeHealthProbe([])).Audit(baseline);
        Assert.Equal(AuditOutcome.Fail, Outcome(fail, "ADV"));
        Assert.Equal(0, fail.Score);
        Assert.Contains(TweakId, fail.Results.Single(r => string.Equals(r.RuleId, "ADV", StringComparison.Ordinal)).Evidence, StringComparison.Ordinal);
    }
}
