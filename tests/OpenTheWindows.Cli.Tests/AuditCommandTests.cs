using System.CommandLine;
using System.Text.Json;
using OpenTheWindows.Core;

namespace OpenTheWindows.Cli.Tests;

public sealed class AuditCommandTests
{
    private static int Invoke(RootCommand root, StringWriter stdout, StringWriter stderr, params string[] args)
        => CliTestHost.Invoke(root, stdout, stderr, args);

    private static (int Exit, string Stdout) ValidateBaselineDir(string json)
    {
        string dir = Path.Combine(Path.GetTempPath(), "otw-baseline-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "bad.json"), json);
        try
        {
            (RootCommand root, StringWriter stdout, StringWriter stderr) = CliTestHost.DefaultHost();
            int exit = Invoke(root, stdout, stderr, "audit", "validate", "--baseline-dir", dir);
            return (exit, stdout.ToString());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

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
        Assert.True(doc.RootElement.GetProperty("ruleCount").GetInt32() > 0);
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

        (int exit, string text) = ValidateBaselineDir(Json);

        Assert.Equal(ExitCodes.InvalidInput, exit);
        Assert.Contains("Baselines INVALID:", text, StringComparison.Ordinal);
        Assert.Contains("unknown-tweak", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_baseline_that_fails_schema_exits_four()
    {
        const string Json = """
        {
          "schemaVersion": 1,
          "id": "bad-schema",
          "title": "Broken baseline",
          "rules": [
            { "ruleId": "BAD-1", "title": "Bad severity", "severity": "NOPE", "tweakIds": [], "sources": ["https://example.test/bad"] }
          ]
        }
        """;

        (int exit, string text) = ValidateBaselineDir(Json);

        Assert.Equal(ExitCodes.InvalidInput, exit);
        Assert.Contains("schema", text, StringComparison.Ordinal);
    }
}
