using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Audit;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Tests.Audit;

public sealed class BaselineValidatorTests
{
    private static TweakCatalog Catalog() => AuditTestCatalog.LoadBuiltIn();

    private static BaselineRule Rule(
        string ruleId,
        string title = "A control",
        BaselineSeverity severity = BaselineSeverity.CatII,
        IReadOnlyList<TweakId>? tweakIds = null,
        BaselineCheck? checkOnly = null,
        IReadOnlyList<Uri>? sources = null)
        => new(ruleId, title, severity, tweakIds ?? [], checkOnly, sources ?? [new Uri("https://example.test/control")]);

    private static Baseline Baseline(params BaselineRule[] rules)
        => new(null, 1, "unit-test", "Unit Test Baseline", rules);

    [Fact]
    public void A_well_formed_baseline_has_no_errors()
    {
        Baseline baseline = Baseline(
            Rule("R-1", tweakIds: [new TweakId("security.network.remove-smbv1")]),
            Rule("R-2", checkOnly: new BaselineCheck(HealthCheckIds.SecureBoot)));

        IReadOnlyList<CatalogIssue> issues = BaselineValidator.Validate(baseline, "unit-test", Catalog(), HealthCheckIds.All);

        Assert.DoesNotContain(issues, i => i.Severity == CatalogIssueSeverity.Error);
    }

    [Fact]
    public void An_unknown_tweak_id_is_an_error()
    {
        Baseline baseline = Baseline(Rule("R-1", tweakIds: [new TweakId("security.network.no-such-tweak")]));

        IReadOnlyList<CatalogIssue> issues = BaselineValidator.Validate(baseline, "unit-test", Catalog(), HealthCheckIds.All);

        Assert.Contains(issues, i => string.Equals(i.Rule, "unknown-tweak", StringComparison.Ordinal));
    }

    [Fact]
    public void An_unknown_health_check_id_is_an_error()
    {
        Baseline baseline = Baseline(Rule("R-1", checkOnly: new BaselineCheck("boot.no-such-check")));

        IReadOnlyList<CatalogIssue> issues = BaselineValidator.Validate(baseline, "unit-test", Catalog(), HealthCheckIds.All);

        Assert.Contains(issues, i => string.Equals(i.Rule, "unknown-health-check", StringComparison.Ordinal));
    }

    [Fact]
    public void A_non_https_source_is_an_error()
    {
        Baseline baseline = Baseline(Rule("R-1",
            tweakIds: [new TweakId("security.network.remove-smbv1")],
            sources: [new Uri("http://example.test/control")]));

        IReadOnlyList<CatalogIssue> issues = BaselineValidator.Validate(baseline, "unit-test", Catalog(), HealthCheckIds.All);

        Assert.Contains(issues, i => string.Equals(i.Rule, "source-https", StringComparison.Ordinal));
    }

    [Fact]
    public void A_rule_with_no_sources_is_an_error()
    {
        Baseline baseline = Baseline(Rule("R-1",
            tweakIds: [new TweakId("security.network.remove-smbv1")],
            sources: []));

        IReadOnlyList<CatalogIssue> issues = BaselineValidator.Validate(baseline, "unit-test", Catalog(), HealthCheckIds.All);

        Assert.Contains(issues, i => string.Equals(i.Rule, "sources-required", StringComparison.Ordinal));
    }

    [Fact]
    public void A_duplicate_rule_id_is_an_error()
    {
        Baseline baseline = Baseline(
            Rule("R-1", tweakIds: [new TweakId("security.network.remove-smbv1")]),
            Rule("R-1", checkOnly: new BaselineCheck(HealthCheckIds.SecureBoot)));

        IReadOnlyList<CatalogIssue> issues = BaselineValidator.Validate(baseline, "unit-test", Catalog(), HealthCheckIds.All);

        Assert.Contains(issues, i => string.Equals(i.Rule, "unique-rule-id", StringComparison.Ordinal));
    }

    [Fact]
    public void An_empty_rule_title_is_an_error()
    {
        Baseline baseline = Baseline(Rule("R-1", title: "  ", tweakIds: [new TweakId("security.network.remove-smbv1")]));

        IReadOnlyList<CatalogIssue> issues = BaselineValidator.Validate(baseline, "unit-test", Catalog(), HealthCheckIds.All);

        Assert.Contains(issues, i => string.Equals(i.Rule, "title-required", StringComparison.Ordinal));
    }

    [Fact]
    public void A_manual_rule_with_no_tweaks_or_check_is_allowed()
    {
        Baseline baseline = Baseline(Rule("R-manual"));

        IReadOnlyList<CatalogIssue> issues = BaselineValidator.Validate(baseline, "unit-test", Catalog(), HealthCheckIds.All);

        Assert.DoesNotContain(issues, i => i.Severity == CatalogIssueSeverity.Error);
    }
}
