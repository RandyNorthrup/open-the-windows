using System.Text.Json;
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

    // The single-registry advertising-id tweak, wrapped in a one-rule baseline, plus its action.
    private static (Baseline Baseline, RegistryAction Registry) AdvertisingId()
    {
        const string TweakId = "privacy.advertising-id.disable";
        RegistryAction registry = Catalog().Get(new TweakId(TweakId)).Actions.OfType<RegistryAction>().First();
        Baseline baseline = new(null, 1, "unit", "Unit Baseline",
            [new BaselineRule("ADV", "Disable the advertising id", BaselineSeverity.CatII, [new TweakId(TweakId)], null, [Source])]);
        return (baseline, registry);
    }

    // Fake readers that return <paramref name="whenMatched"/> for the tweak's registry value and "absent" otherwise.
    private static FakeReaders Readers(RegistryAction registry, RegistryValueSnapshot whenMatched, ManagedState managed = ManagedState.NotManaged)
        => new()
        {
            OnInteractiveUser = () => new InteractiveUser("S-1-5-21-1", "Tester", true),
            OnManaged = (_, _, _) => managed,
            OnRegistry = (_, _, path, name) =>
                string.Equals(path, registry.Path, StringComparison.Ordinal) && string.Equals(name, registry.Name, StringComparison.Ordinal)
                    ? whenMatched
                    : new RegistryValueSnapshot(false, null, null),
        };

    [Fact]
    public void A_compliant_tweak_passes_and_a_drifting_tweak_fails()
    {
        (Baseline baseline, RegistryAction registry) = AdvertisingId();

        AuditReport pass = Engine(Readers(registry, new RegistryValueSnapshot(true, registry.Type, registry.Value)), new FakeHealthProbe([])).Audit(baseline);
        Assert.Equal(AuditOutcome.Pass, Outcome(pass, "ADV"));
        Assert.Equal(100, pass.Score);

        AuditReport fail = Engine(Readers(registry, new RegistryValueSnapshot(false, null, null)), new FakeHealthProbe([])).Audit(baseline);
        Assert.Equal(AuditOutcome.Fail, Outcome(fail, "ADV"));
        Assert.Equal(0, fail.Score);
        Assert.Contains("privacy.advertising-id.disable", fail.Results.Single(r => string.Equals(r.RuleId, "ADV", StringComparison.Ordinal)).Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public void A_policy_managed_tweak_passes_at_the_desired_value_and_fails_at_a_different_value()
    {
        (Baseline baseline, RegistryAction registry) = AdvertisingId();

        // Managed by group policy, at the desired value → the control is satisfied.
        AuditReport atDesired = Engine(
            Readers(registry, new RegistryValueSnapshot(true, registry.Type, registry.Value), ManagedState.GroupPolicy),
            new FakeHealthProbe([])).Audit(baseline);
        Assert.Equal(AuditOutcome.Pass, Outcome(atDesired, "ADV"));

        // Managed by group policy, at a different value → a real failure.
        using var otherValue = JsonDocument.Parse("1");
        AuditReport mismatch = Engine(
            Readers(registry, new RegistryValueSnapshot(true, registry.Type, otherValue.RootElement), ManagedState.GroupPolicy),
            new FakeHealthProbe([])).Audit(baseline);
        Assert.Equal(AuditOutcome.Fail, Outcome(mismatch, "ADV"));
    }
}
