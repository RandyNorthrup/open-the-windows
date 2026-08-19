using System.CommandLine;
using System.Security.Cryptography;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

public sealed class AuditCommandTests
{
    // A baseline with a single check-only rule, so a fake health probe fully
    // determines the outcome (and therefore the exit code and score).
    private const string OneCheckBaseline = """
    {
      "schemaVersion": 1,
      "id": "unit-test",
      "title": "Unit Test Baseline",
      "rules": [
        { "ruleId": "R1", "title": "Secure Boot enabled", "severity": "CAT I", "tweakIds": [], "checkOnly": { "healthCheckId": "boot.secure-boot" }, "sources": ["https://example.test/x"] }
      ]
    }
    """;

    private static int Invoke(RootCommand root, StringWriter stdout, StringWriter stderr, params string[] args)
        => CliTestHost.Invoke(root, stdout, stderr, args);

    private static (RootCommand Root, StringWriter Out, StringWriter Err) HealthHost(IMachineHealthProbe health)
    {
        var doctor = new DoctorService(
            new FakeOperatingSystemInfo(FakeOperatingSystemInfo.Windows11Pro24H2()),
            new FakeElevationContext(isElevated: true));
        return CliTestHost.Host(TestCli.Services(doctor, _ => CatalogLoader.LoadBuiltIn(), health: health));
    }

    private static FakeHealthProbe Probe(HealthStatus status)
        => new([new HealthCheckResult("boot.secure-boot", "Secure Boot", status, status.ToString(), null)]);

    private static string TempFile(string content, string extension)
    {
        string path = Path.Combine(Path.GetTempPath(), "otw-audit-tests", Guid.NewGuid().ToString("N") + extension);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static string TempKeyFile()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return TempFile(key.ExportECPrivateKeyPem(), ".pem");
    }

    // ----- run ---------------------------------------------------------------

    [Fact]
    public void Run_exits_success_and_scores_100_when_the_check_passes()
    {
        string baseline = TempFile(OneCheckBaseline, ".json");
        try
        {
            (RootCommand root, StringWriter stdout, StringWriter stderr) = HealthHost(Probe(HealthStatus.Pass));

            int exit = Invoke(root, stdout, stderr, "audit", "run", "--baseline", baseline);

            Assert.Equal(ExitCodes.Success, exit);
            Assert.Contains("Score: 100 / 100", stdout.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(baseline);
        }
    }

    [Fact]
    public void Run_exits_drift_and_lists_the_failure_when_the_check_fails()
    {
        string baseline = TempFile(OneCheckBaseline, ".json");
        try
        {
            (RootCommand root, StringWriter stdout, StringWriter stderr) = HealthHost(Probe(HealthStatus.Fail));

            int exit = Invoke(root, stdout, stderr, "audit", "run", "--baseline", baseline);

            Assert.Equal(ExitCodes.Drift, exit);
            string text = stdout.ToString();
            Assert.Contains("Score: 0 / 100", text, StringComparison.Ordinal);
            Assert.Contains("FAIL  [CAT I] R1", text, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(baseline);
        }
    }

    [Fact]
    public void Run_json_emits_a_report_with_an_integrity_hash()
    {
        string baseline = TempFile(OneCheckBaseline, ".json");
        try
        {
            (RootCommand root, StringWriter stdout, StringWriter stderr) = HealthHost(Probe(HealthStatus.Pass));

            int exit = Invoke(root, stdout, stderr, "audit", "run", "--baseline", baseline, "--json");

            Assert.Equal(ExitCodes.Success, exit);
            using var doc = JsonDocument.Parse(stdout.ToString());
            Assert.Equal(64, doc.RootElement.GetProperty("hash").GetString()!.Length);
            Assert.Equal("unit-test", doc.RootElement.GetProperty("report").GetProperty("baselineId").GetString());
        }
        finally
        {
            File.Delete(baseline);
        }
    }

    [Fact]
    public void Run_out_writes_the_report_to_a_file()
    {
        string baseline = TempFile(OneCheckBaseline, ".json");
        string outPath = TempFile(string.Empty, ".sarif");
        try
        {
            (RootCommand root, StringWriter stdout, StringWriter stderr) = HealthHost(Probe(HealthStatus.Fail));

            int exit = Invoke(root, stdout, stderr, "audit", "run", "--baseline", baseline, "--sarif", "--out", outPath);

            Assert.Equal(ExitCodes.Drift, exit);
            Assert.Contains("Wrote sarif report to", stdout.ToString(), StringComparison.Ordinal);
            Assert.Contains("\"version\": \"2.1.0\"", File.ReadAllText(outPath), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(baseline);
            File.Delete(outPath);
        }
    }

    [Fact]
    public void Run_unknown_baseline_exits_four()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = HealthHost(Probe(HealthStatus.Pass));

        int exit = Invoke(root, stdout, stderr, "audit", "run", "--baseline", "no-such-baseline");

        Assert.Equal(ExitCodes.InvalidInput, exit);
        Assert.Contains("Unknown baseline", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_two_formats_exits_error()
    {
        string baseline = TempFile(OneCheckBaseline, ".json");
        try
        {
            (RootCommand root, StringWriter stdout, StringWriter stderr) = HealthHost(Probe(HealthStatus.Pass));

            int exit = Invoke(root, stdout, stderr, "audit", "run", "--baseline", baseline, "--json", "--csv");

            Assert.Equal(ExitCodes.Error, exit);
            Assert.Contains("at most one", stderr.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(baseline);
        }
    }

    // Runs `audit run --baseline <one-check baseline> --sign <key> [extraArgs]` with a passing check.
    private static (int Exit, string Stdout, string Stderr) RunSign(params string[] extraArgs)
    {
        string baseline = TempFile(OneCheckBaseline, ".json");
        string key = TempKeyFile();
        try
        {
            (RootCommand root, StringWriter stdout, StringWriter stderr) = HealthHost(Probe(HealthStatus.Pass));
            string[] args = ["audit", "run", "--baseline", baseline, "--sign", key, .. extraArgs];
            int exit = Invoke(root, stdout, stderr, args);
            return (exit, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            File.Delete(baseline);
            File.Delete(key);
        }
    }

    [Fact]
    public void Run_sign_with_a_non_json_format_exits_error()
    {
        (int exit, _, string stderr) = RunSign("--csv");

        Assert.Equal(ExitCodes.Error, exit);
        Assert.Contains("only available with the JSON format", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_sign_emits_a_signed_report()
    {
        (int exit, string stdout, _) = RunSign();

        Assert.Equal(ExitCodes.Success, exit);
        using var doc = JsonDocument.Parse(stdout);
        Assert.Equal("ES256", doc.RootElement.GetProperty("signature").GetProperty("alg").GetString());
    }

    // ----- list --------------------------------------------------------------

    [Fact]
    public void List_prints_the_three_built_in_baselines()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = CliTestHost.DefaultHost();

        int exit = Invoke(root, stdout, stderr, "audit", "list");

        Assert.Equal(ExitCodes.Success, exit);
        string text = stdout.ToString();
        Assert.Contains("disa-stig-v2r7", text, StringComparison.Ordinal);
        Assert.Contains("cis-win11-ent-v4.0", text, StringComparison.Ordinal);
        Assert.Contains("ms-baseline-25h2", text, StringComparison.Ordinal);
    }

    [Fact]
    public void List_json_emits_an_array_of_three_summaries()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = CliTestHost.DefaultHost();

        int exit = Invoke(root, stdout, stderr, "audit", "list", "--json");

        Assert.Equal(ExitCodes.Success, exit);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.Equal(3, doc.RootElement.GetArrayLength());
        Assert.True(doc.RootElement[0].GetProperty("ruleCount").GetInt32() > 0);
    }

    // ----- validate ----------------------------------------------------------

    [Fact]
    public void Validate_built_in_baselines_exits_zero()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = CliTestHost.DefaultHost();

        int exit = Invoke(root, stdout, stderr, "audit", "validate");

        Assert.Equal(ExitCodes.Success, exit);
        Assert.Contains("Baselines valid:", stdout.ToString(), StringComparison.Ordinal);
        Assert.Empty(stderr.ToString());
    }

    [Fact]
    public void Validate_json_emits_a_report()
    {
        (RootCommand root, StringWriter stdout, StringWriter stderr) = CliTestHost.DefaultHost();

        int exit = Invoke(root, stdout, stderr, "audit", "validate", "--json");

        Assert.Equal(ExitCodes.Success, exit);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.True(doc.RootElement.GetProperty("isValid").GetBoolean());
        Assert.Equal(3, doc.RootElement.GetProperty("baselineCount").GetInt32());
    }

    [Fact]
    public void Validate_baseline_with_unknown_tweak_exits_four()
    {
        const string Json = """
        {
          "schemaVersion": 1,
          "id": "bad-baseline",
          "title": "Broken baseline",
          "rules": [
            { "ruleId": "BAD-1", "title": "References an unknown tweak", "severity": "CAT I", "tweakIds": ["security.network.no-such-tweak-xyz"], "sources": ["https://example.test/bad"] }
          ]
        }
        """;
        string dir = Path.Combine(Path.GetTempPath(), "otw-audit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "bad.json"), Json);
        try
        {
            (RootCommand root, StringWriter stdout, StringWriter stderr) = CliTestHost.DefaultHost();

            int exit = Invoke(root, stdout, stderr, "audit", "validate", "--baseline-dir", dir);

            Assert.Equal(ExitCodes.InvalidInput, exit);
            Assert.Contains("unknown-tweak", stdout.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
