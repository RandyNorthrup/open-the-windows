using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Audit;
using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Tests.Audit;

public sealed class BuiltInBaselinesTests
{
    private static readonly string[] ExpectedIds =
    [
        "cis-win11-ent-v4.0",
        "disa-stig-v2r7",
        "ms-baseline-25h2",
    ];

    private static TweakCatalog Catalog() => AuditTestCatalog.LoadBuiltIn();

    [Fact]
    public void Built_in_ids_are_the_expected_three()
    {
        Assert.Equal(ExpectedIds, BaselineLoader.BuiltInIds);
    }

    [Fact]
    public void All_built_in_baselines_load_without_errors()
    {
        BaselineSetLoadResult result = BaselineLoader.LoadBuiltIn();

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(e => e.ToString())));
        Assert.Equal(ExpectedIds.Length, result.Baselines.Count);
    }

    [Fact]
    public void Every_built_in_passes_the_structural_validator()
    {
        TweakCatalog catalog = Catalog();

        foreach (Baseline baseline in BaselineLoader.LoadBuiltIn().Baselines)
        {
            IReadOnlyList<CatalogIssue> issues = BaselineValidator.Validate(baseline, baseline.Id, catalog, HealthCheckIds.All);
            Assert.DoesNotContain(issues, i => i.Severity == CatalogIssueSeverity.Error);
        }
    }

    [Fact]
    public void Every_built_in_has_rules()
    {
        foreach (Baseline baseline in BaselineLoader.LoadBuiltIn().Baselines)
        {
            Assert.NotEmpty(baseline.Rules);
        }
    }

    [Fact]
    public void TryLoadBuiltIn_finds_by_id_and_returns_null_for_unknown()
    {
        Assert.Equal("disa-stig-v2r7", BaselineLoader.TryLoadBuiltIn("disa-stig-v2r7")?.Baseline?.Id);
        Assert.Null(BaselineLoader.TryLoadBuiltIn("no-such-baseline"));
    }
}
