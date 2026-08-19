using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Audit;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Core.Tests.Audit;

/// <summary>Golden / validation tests for the four audit report writers and report integrity.</summary>
public sealed class AuditReportWriterTests
{
    private static AuditReport SampleReport()
    {
        var os = new OperatingSystemFacts("Windows 11 Pro", "Professional", "24H2", 26100, 4351, "x64", "Client");
        AuditRuleResult[] results =
        [
            new("V-1", "Disable SMBv1", BaselineSeverity.CatI, AuditOutcome.Fail,
                [new TweakId("security.network.remove-smbv1")], "security.network.remove-smbv1: (absent)"),
            new("V-2", "Secure Boot enabled", BaselineSeverity.CatII, AuditOutcome.Pass, [], "Secure Boot is enabled."),
            new("V-3", "Manual review", BaselineSeverity.CatIII, AuditOutcome.Manual, [], "Manual check."),
            new("V-4", "Cloud protection", BaselineSeverity.Baseline, AuditOutcome.Unknown, [], "did not run"),
            new("V-5,special", "Has \"quotes\"", BaselineSeverity.L2, AuditOutcome.NotApplicable, [], "n/a"),
        ];
        var at = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
        return new AuditReport("disa-stig-v2r7", "DISA STIG sample", at, os,
            AuditScoring.Score(results), AuditSummary.From(results), results);
    }

    private static string Render(IAuditReportWriter writer, AuditReport report)
    {
        using var stream = new MemoryStream();
        writer.Write(report, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public void The_sample_score_is_weighted()
    {
        // Scoreable: V-1 Fail (CAT I, 10) + V-2 Pass (CAT II, 5) => 5 / 15 => 33.
        Assert.Equal(33, SampleReport().Score);
    }

    [Fact]
    public void Csv_matches_golden()
    {
        const string Expected =
            "ruleId,severity,outcome,title,tweakIds,evidence\n" +
            "V-1,CAT I,Fail,Disable SMBv1,security.network.remove-smbv1,security.network.remove-smbv1: (absent)\n" +
            "V-2,CAT II,Pass,Secure Boot enabled,,Secure Boot is enabled.\n" +
            "V-3,CAT III,Manual,Manual review,,Manual check.\n" +
            "V-4,Baseline,Unknown,Cloud protection,,did not run\n" +
            "\"V-5,special\",L2,NotApplicable,\"Has \"\"quotes\"\"\",,n/a\n";

        Assert.Equal(Expected, Render(new AuditCsvWriter(), SampleReport()));
    }

    [Fact]
    public void Unsigned_json_has_a_hash_and_no_signature()
    {
        string json = Render(new AuditJsonWriter(), SampleReport());

        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.Equal(64, root.GetProperty("hash").GetString()!.Length);
        Assert.False(root.TryGetProperty("signature", out _));

        JsonElement report = root.GetProperty("report");
        Assert.Equal("disa-stig-v2r7", report.GetProperty("baselineId").GetString());
        Assert.Equal(33, report.GetProperty("score").GetInt32());
        JsonElement first = report.GetProperty("results")[0];
        Assert.Equal("V-1", first.GetProperty("ruleId").GetString());
        Assert.Equal("CAT I", first.GetProperty("severity").GetString());
        Assert.Equal("Fail", first.GetProperty("outcome").GetString());
    }

    [Fact]
    public void Signed_json_round_trips_and_verifies()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string json = Render(new AuditJsonWriter(key), SampleReport());

        SignedAuditReport signed = JsonSerializer.Deserialize(json, AuditReportJsonContext.Default.SignedAuditReport)!;
        Assert.NotNull(signed.Signature);
        Assert.Equal(ProfileSignature.Algorithm, signed.Signature.Algorithm);
        Assert.True(AuditReportIntegrity.Verify(signed, key));

        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.False(AuditReportIntegrity.Verify(signed, other));
    }

    [Fact]
    public void A_tampered_report_fails_verification()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        AuditReport report = SampleReport();
        string json = Render(new AuditJsonWriter(key), report);
        SignedAuditReport signed = JsonSerializer.Deserialize(json, AuditReportJsonContext.Default.SignedAuditReport)!;

        SignedAuditReport tampered = signed with { Report = report with { Score = 100 } };

        Assert.False(AuditReportIntegrity.Verify(tampered, key));
        Assert.False(AuditReportIntegrity.Verify(tampered, null));
    }

    [Fact]
    public void Html_is_self_contained_and_groups_by_severity()
    {
        string html = Render(new AuditHtmlWriter(), SampleReport());

        Assert.StartsWith("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.Contains(AppInfo.Title, html, StringComparison.Ordinal);
        Assert.Contains("Score: 33 / 100", html, StringComparison.Ordinal);
        Assert.Contains("<h2>CAT I</h2>", html, StringComparison.Ordinal);
        Assert.Contains("class=\"fail\"", html, StringComparison.Ordinal);
        // No external assets: strict CSP-style self-containment.
        Assert.DoesNotContain("http://", html, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sarif_lists_every_rule_and_only_failures_as_results()
    {
        string sarif = Render(new AuditSarifWriter(), SampleReport());

        using var document = JsonDocument.Parse(sarif);
        JsonElement root = document.RootElement;
        Assert.Equal("2.1.0", root.GetProperty("version").GetString());

        JsonElement run = root.GetProperty("runs")[0];
        Assert.Equal(AppInfo.Title, run.GetProperty("tool").GetProperty("driver").GetProperty("name").GetString());
        Assert.Equal(5, run.GetProperty("tool").GetProperty("driver").GetProperty("rules").GetArrayLength());

        JsonElement results = run.GetProperty("results");
        Assert.Equal(1, results.GetArrayLength());
        Assert.Equal("V-1", results[0].GetProperty("ruleId").GetString());
        Assert.Equal("error", results[0].GetProperty("level").GetString());
    }

    [Fact]
    public void Writers_expose_their_extension()
    {
        Assert.Equal("json", new AuditJsonWriter().Extension);
        Assert.Equal("csv", new AuditCsvWriter().Extension);
        Assert.Equal("html", new AuditHtmlWriter().Extension);
        Assert.Equal("sarif", new AuditSarifWriter().Extension);
    }
}
