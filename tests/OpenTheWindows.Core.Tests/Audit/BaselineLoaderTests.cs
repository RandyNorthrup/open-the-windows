using System.Text.Json;
using OpenTheWindows.Core.Audit;

namespace OpenTheWindows.Core.Tests.Audit;

public sealed class BaselineLoaderTests
{
    private const string ValidBaselineJson = """
    {
      "$schema": "https://github.com/RandyNorthrup/open-the-windows/catalog/schema/baseline.schema.json",
      "schemaVersion": 1,
      "id": "unit-test",
      "title": "Unit Test Baseline",
      "rules": [
        { "ruleId": "R-1", "title": "SMBv1 removed", "severity": "CAT I", "tweakIds": ["security.network.remove-smbv1"], "sources": ["https://example.test/smb"] },
        { "ruleId": "R-2", "title": "Secure Boot enabled", "severity": "Baseline", "tweakIds": [], "checkOnly": { "healthCheckId": "boot.secure-boot" }, "sources": ["https://example.test/secure-boot"] }
      ]
    }
    """;

    [Fact]
    public void Valid_baseline_loads_with_every_field()
    {
        BaselineLoadResult result = BaselineLoader.LoadText("unit-test.json", ValidBaselineJson);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(e => e.ToString())));
        Baseline baseline = result.Baseline!;
        Assert.Equal("unit-test", baseline.Id);
        Assert.Equal("Unit Test Baseline", baseline.Title);
        Assert.Equal(2, baseline.Rules.Count);
        Assert.Equal(BaselineSeverity.CatI, baseline.Rules[0].Severity);
        Assert.Equal("security.network.remove-smbv1", baseline.Rules[0].TweakIds[0].Value);
        Assert.Null(baseline.Rules[0].CheckOnly);
        Assert.Equal("boot.secure-boot", baseline.Rules[1].CheckOnly!.HealthCheckId);
        Assert.Empty(baseline.Rules[1].TweakIds);
    }

    // One row per way a baseline document is rejected: an unknown member, a missing
    // required field, an unknown severity value (all caught by the JSON Schema,
    // rule "schema"), and an unsupported schemaVersion (caught after, "schema-version").
    [Theory]
    [InlineData("\"schemaVersion\": 1,", "\"schemaVersion\": 1, \"bogus\": 1,", "schema")]
    [InlineData("\"title\": \"Unit Test Baseline\",", "", "schema")]
    [InlineData("\"severity\": \"CAT I\"", "\"severity\": \"CAT 9\"", "schema")]
    [InlineData("\"schemaVersion\": 1,", "\"schemaVersion\": 2,", "schema-version")]
    public void A_mutated_baseline_is_rejected(string find, string replacement, string expectedRule)
    {
        string json = ValidBaselineJson.Replace(find, replacement, StringComparison.Ordinal);

        BaselineLoadResult result = BaselineLoader.LoadText("unit-test.json", json);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => string.Equals(e.Rule, expectedRule, StringComparison.Ordinal));
    }

    [Fact]
    public void Malformed_json_is_reported_not_thrown()
    {
        BaselineLoadResult result = BaselineLoader.LoadText("unit-test.json", "{ not json ");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => string.Equals(e.Rule, "json", StringComparison.Ordinal));
    }

    [Fact]
    public void A_missing_file_is_reported_not_thrown()
    {
        BaselineLoadResult result = BaselineLoader.LoadFile(Path.Combine(Path.GetTempPath(), "otw-no-such-baseline-xyz.json"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => string.Equals(e.Rule, "file-missing", StringComparison.Ordinal));
    }

    [Fact]
    public void A_loaded_baseline_round_trips_through_json()
    {
        Baseline original = BaselineLoader.LoadText("unit-test.json", ValidBaselineJson).Baseline!;

        string json = JsonSerializer.Serialize(original, BaselineJsonContext.Default.Baseline);
        Baseline reloaded = BaselineLoader.LoadText("round-trip.json", json).Baseline!;

        Assert.Equal(original.Id, reloaded.Id);
        Assert.Equal(original.Rules.Count, reloaded.Rules.Count);
        Assert.Equal(original.Rules[0].Severity, reloaded.Rules[0].Severity);
        Assert.Equal(original.Rules[1].CheckOnly!.HealthCheckId, reloaded.Rules[1].CheckOnly!.HealthCheckId);
    }
}
