using System.Text;
using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Reports;

namespace OpenTheWindows.Core.Tests.Reports;

/// <summary>
/// Golden / validation tests for the four report writers over one fixed report.
/// </summary>
public sealed class ReportWriterTests
{
    private static ScanReport SampleReport()
    {
        var os = new OperatingSystemFacts("Windows 11 Pro", "Professional", "24H2", 26100, 4351, "x64", "Client");
        var action = new FakeAction("Registry");
        var drift = new TweakObservation(
            new TweakId("privacy.telemetry.allow"),
            ComplianceState.Drift,
            RiskTier.Breaking,
            [new ActionObservation(action, ComplianceState.Drift, "1", "0", ManagedState.NotManaged)]);
        var compliant = new TweakObservation(
            new TweakId("privacy.ads.disable"),
            ComplianceState.Compliant,
            RiskTier.Safe,
            [new ActionObservation(action, ComplianceState.Compliant, "0", "0", ManagedState.NotManaged)]);
        var notApplicable = new TweakObservation(
            new TweakId("server.only.tweak"),
            ComplianceState.NotApplicable,
            RiskTier.Caution,
            []);

        var at = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
        return new ScanReport(at, os, "balanced", [drift, compliant, notApplicable]);
    }

    private static string Render(IReportWriter writer, ScanReport report)
    {
        using var stream = new MemoryStream();
        writer.Write(report, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public void Csv_matches_golden()
    {
        const string Expected =
            "id,state,risk,kind,actionState,actual,desired,managed\n" +
            "privacy.telemetry.allow,Drift,Breaking,Registry,Drift,1,0,NotManaged\n" +
            "privacy.ads.disable,Compliant,Safe,Registry,Compliant,0,0,NotManaged\n" +
            "server.only.tweak,NotApplicable,Caution,,,,,\n";

        Assert.Equal(Expected, Render(new CsvReportWriter(), SampleReport()));
    }

    [Fact]
    public void Json_parses_with_expected_fields()
    {
        string json = Render(new JsonReportWriter(), SampleReport());

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("balanced", root.GetProperty("profile").GetString());
        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());

        JsonElement summary = root.GetProperty("summary");
        Assert.Equal(3, summary.GetProperty("total").GetInt32());
        Assert.Equal(1, summary.GetProperty("drift").GetInt32());
        Assert.Equal(1, summary.GetProperty("compliant").GetInt32());
        Assert.Equal(1, summary.GetProperty("notApplicable").GetInt32());
        Assert.False(summary.GetProperty("isCompliant").GetBoolean());

        JsonElement first = root.GetProperty("results")[0];
        Assert.Equal("privacy.telemetry.allow", first.GetProperty("id").GetString());
        Assert.Equal("Drift", first.GetProperty("state").GetString());
        Assert.Equal("Breaking", first.GetProperty("risk").GetString());
        Assert.Equal("Registry", first.GetProperty("actions")[0].GetProperty("kind").GetString());
    }

    [Fact]
    public void Html_is_self_contained()
    {
        string html = Render(new HtmlReportWriter(), SampleReport());

        Assert.StartsWith("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.Contains(AppInfo.Title, html, StringComparison.Ordinal);
        Assert.Contains("privacy.telemetry.allow", html, StringComparison.Ordinal);
        Assert.Contains("Drift", html, StringComparison.Ordinal);
        // No external assets: strict CSP-style self-containment.
        Assert.DoesNotContain("http://", html, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sarif_has_one_rule_per_drift_id_and_risk_level()
    {
        string sarif = Render(new SarifReportWriter(), SampleReport());

        using var document = JsonDocument.Parse(sarif);
        var root = document.RootElement;
        Assert.Equal("2.1.0", root.GetProperty("version").GetString());

        var run = root.GetProperty("runs")[0];
        Assert.Equal(AppInfo.Title, run.GetProperty("tool").GetProperty("driver").GetProperty("name").GetString());

        JsonElement rules = run.GetProperty("tool").GetProperty("driver").GetProperty("rules");
        Assert.Equal(1, rules.GetArrayLength());
        Assert.Equal("privacy.telemetry.allow", rules[0].GetProperty("id").GetString());

        JsonElement results = run.GetProperty("results");
        Assert.Equal(1, results.GetArrayLength());
        Assert.Equal("privacy.telemetry.allow", results[0].GetProperty("ruleId").GetString());
        Assert.Equal("error", results[0].GetProperty("level").GetString());
    }

    [Fact]
    public void Writers_expose_their_extension()
    {
        Assert.Equal("json", new JsonReportWriter().Extension);
        Assert.Equal("csv", new CsvReportWriter().Extension);
        Assert.Equal("html", new HtmlReportWriter().Extension);
        Assert.Equal("sarif", new SarifReportWriter().Extension);
    }

    // A report covering every compliance state, risk tier, a managed action, a
    // multi-action entry, an entry with no actions, and CSV-special characters.
    private static ScanReport VariedReport()
    {
        var os = new OperatingSystemFacts("Windows 11 Pro", "Professional", "24H2", 26100, 4351, "x64", "Client");
        var action = new FakeAction("Registry");
        TweakObservation[] results =
        [
            new(new TweakId("a.drift.breaking"), ComplianceState.Drift, RiskTier.Breaking,
                [new ActionObservation(action, ComplianceState.Drift, "a,b", "c\"d", ManagedState.NotManaged)]),
            new(new TweakId("a.drift.advanced"), ComplianceState.Drift, RiskTier.Advanced,
                [new ActionObservation(action, ComplianceState.Drift, "1", "0", ManagedState.NotManaged)]),
            new(new TweakId("a.drift.caution"), ComplianceState.Drift, RiskTier.Caution,
                [new ActionObservation(action, ComplianceState.Drift, "1", "0", ManagedState.NotManaged)]),
            new(new TweakId("a.managed.entry"), ComplianceState.Managed, RiskTier.Caution,
            [
                new ActionObservation(action, ComplianceState.Managed, "on", "off", ManagedState.GroupPolicy),
                new ActionObservation(action, ComplianceState.Compliant, "off", "off", ManagedState.NotManaged),
            ]),
            new(new TweakId("a.unknown.entry"), ComplianceState.Unknown, RiskTier.Safe,
                [new ActionObservation(action, ComplianceState.Unknown, "?", "off", ManagedState.NotManaged)]),
            new(new TweakId("a.notapplicable.entry"), ComplianceState.NotApplicable, RiskTier.Safe, []),
        ];
        return new ScanReport(new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero), os, "strict", results);
    }

    [Fact]
    public void Json_summary_counts_every_state()
    {
        string json = Render(new JsonReportWriter(), VariedReport());

        using var document = JsonDocument.Parse(json);
        JsonElement summary = document.RootElement.GetProperty("summary");
        Assert.Equal(6, summary.GetProperty("total").GetInt32());
        Assert.Equal(3, summary.GetProperty("drift").GetInt32());
        Assert.Equal(1, summary.GetProperty("managed").GetInt32());
        Assert.Equal(1, summary.GetProperty("unknown").GetInt32());
        Assert.Equal(1, summary.GetProperty("notApplicable").GetInt32());
        Assert.Equal(0, summary.GetProperty("compliant").GetInt32());
    }

    [Fact]
    public void Csv_quotes_special_characters()
    {
        string csv = Render(new CsvReportWriter(), VariedReport());

        // Commas are wrapped and quotes doubled; the no-action entry has empty action columns.
        Assert.Contains("\"a,b\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"c\"\"d\"", csv, StringComparison.Ordinal);
        Assert.Contains("a.notapplicable.entry,NotApplicable,Safe,,,,,", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_marks_each_state_class()
    {
        string html = Render(new HtmlReportWriter(), VariedReport());

        Assert.Contains("class=\"drift\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"managed\"", html, StringComparison.Ordinal);
        // The not-applicable entry has no actions and renders the em dash placeholder.
        Assert.Contains("—", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Sarif_level_follows_the_risk_tier()
    {
        string sarif = Render(new SarifReportWriter(), VariedReport());

        using var document = JsonDocument.Parse(sarif);
        JsonElement results = document.RootElement.GetProperty("runs")[0].GetProperty("results");

        // Only the three drift entries are findings; managed/unknown/n-a are not.
        Assert.Equal(3, results.GetArrayLength());
        Dictionary<string, string> levelById = [];
        for (int i = 0; i < results.GetArrayLength(); i++)
        {
            JsonElement result = results[i];
            levelById[result.GetProperty("ruleId").GetString()!] = result.GetProperty("level").GetString()!;
        }

        Assert.Equal("error", levelById["a.drift.breaking"]);
        Assert.Equal("warning", levelById["a.drift.advanced"]);
        Assert.Equal("note", levelById["a.drift.caution"]);
    }

    private sealed record FakeAction(string Kind) : ITweakAction;
}
