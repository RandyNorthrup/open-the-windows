using System.CommandLine;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Profiles;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

public sealed class ApplyCommandTests
{
    private static DoctorService Doctor(bool supported = true)
        => new(
            new FakeOperatingSystemInfo(supported ? FakeOperatingSystemInfo.Windows11Pro24H2() : FakeOperatingSystemInfo.WindowsServer2025()),
            new FakeElevationContext(isElevated: true));

    private static (RootCommand Root, StringWriter Out, StringWriter Err) Build(
        bool supported = true, bool elevated = true, ApplyEngine? engine = null, FakeReaders? readers = null)
        => CliTestHost.Host(TestCli.Services(
            Doctor(supported),
            _ => CatalogLoader.LoadBuiltIn(),
            readers,
            createApplyEngine: engine is null ? null : () => engine,
            elevation: new FakeElevationContext(elevated)));

    [Fact]
    public void Unsupported_platform_exits_3()
    {
        var (root, stdout, stderr) = Build(supported: false);

        int code = CliTestHost.Invoke(root, stdout, stderr, "apply", "--profile", "basic");

        Assert.Equal(ExitCodes.UnsupportedPlatform, code);
    }

    [Fact]
    public void Unknown_profile_exits_4()
    {
        var (root, stdout, stderr) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "apply", "--profile", "nope");

        Assert.Equal(ExitCodes.InvalidInput, code);
        Assert.Contains("Unknown profile", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Not_elevated_exits_5()
    {
        var (root, stdout, stderr) = Build(elevated: false);

        int code = CliTestHost.Invoke(root, stdout, stderr, "apply", "--profile", "basic", "--include-draft", "--yes");

        Assert.Equal(ExitCodes.ElevationRequired, code);
    }

    [Fact]
    public void What_if_exits_0_and_makes_no_changes()
    {
        var machine = new FakeMachine();
        ApplyHarness harness = FakeApplyEngine.Create(machine);
        var (root, stdout, stderr) = Build(engine: harness.Engine);

        int code = CliTestHost.Invoke(root, stdout, stderr, "apply", "--profile", "basic", "--include-draft", "--what-if");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Equal(0, machine.WriteCount);
        Assert.Empty(harness.Journal.Records);
    }

    [Fact]
    public void Only_without_profile_selects_just_that_entry()
    {
        string id = CatalogLoader.LoadBuiltIn().Catalog!.Entries[0].Id.Value;
        var (root, stdout, stderr) = Build(engine: FakeApplyEngine.Create(new FakeMachine()).Engine);

        int code = CliTestHost.Invoke(root, stdout, stderr, "apply", "--only", id, "--what-if", "--json");

        Assert.Equal(ExitCodes.Success, code);
        using var document = JsonDocument.Parse(stdout.ToString());
        JsonElement entries = document.RootElement.GetProperty("entries");
        Assert.Equal(1, entries.GetArrayLength());
        Assert.Equal(id, entries[0].GetProperty("id").GetString());
    }

    [Fact]
    public void Neither_profile_nor_only_reports_profile_required()
    {
        var (root, stdout, stderr) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "apply", "--what-if");

        Assert.Equal(ExitCodes.InvalidInput, code);
        Assert.Contains("--profile' is required", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void What_if_json_parses()
    {
        var (root, stdout, stderr) = Build(engine: FakeApplyEngine.Create(new FakeMachine()).Engine);

        int code = CliTestHost.Invoke(root, stdout, stderr, "apply", "--profile", "basic", "--include-draft", "--what-if", "--json");

        Assert.Equal(ExitCodes.Success, code);
        using var document = JsonDocument.Parse(stdout.ToString());
        Assert.True(document.RootElement.TryGetProperty("state", out _));
    }

    [Fact]
    public void Apply_completes_and_journals_a_run()
    {
        var machine = new FakeMachine();
        ApplyHarness harness = FakeApplyEngine.Create(machine);
        var (root, stdout, stderr) = Build(engine: harness.Engine);

        int code = CliTestHost.Invoke(root, stdout, stderr, "apply", "--profile", "basic", "--include-draft", "--yes", "--no-restore-point", "--json");

        Assert.True(code is ExitCodes.Success or ExitCodes.RebootRequired, "apply should succeed");
        using var document = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("Completed", document.RootElement.GetProperty("state").GetString());
        Assert.Single(harness.Journal.Records);
    }

    [Fact]
    public void Apply_text_output_reports_the_state()
    {
        var (root, stdout, stderr) = Build(engine: FakeApplyEngine.Create(new FakeMachine()).Engine);

        int code = CliTestHost.Invoke(root, stdout, stderr, "apply", "--profile", "basic", "--include-draft", "--yes", "--no-restore-point");

        Assert.True(code is ExitCodes.Success or ExitCodes.RebootRequired);
        Assert.Contains("Applied", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Built_in_profile_id_resolves_and_plans()
    {
        var (root, stdout, stderr) = Build(engine: FakeApplyEngine.Create(new FakeMachine()).Engine);

        int code = CliTestHost.Invoke(root, stdout, stderr, "apply", "--profile", "home", "--what-if", "--json");

        Assert.Equal(ExitCodes.Success, code);
        using var document = JsonDocument.Parse(stdout.ToString());
        Assert.True(document.RootElement.GetProperty("entries").GetArrayLength() >= 1, "the home profile should resolve to at least one applicable entry");
    }

    [Fact]
    public void Blocking_preflight_exits_2_and_reports_blocked()
    {
        ApplyHarness harness = FakeApplyEngine.Create(new FakeMachine());
        harness.Preflight.Report = new PreflightReport([new PreflightIssue(PreflightCheck.PendingReboot, Blocking: true, "A reboot is pending.")]);
        var (root, stdout, stderr) = Build(engine: harness.Engine);

        int code = CliTestHost.Invoke(root, stdout, stderr, "apply", "--profile", "basic", "--include-draft", "--yes", "--no-restore-point");

        Assert.Equal(ExitCodes.Error, code);
        Assert.Contains("BLOCKED", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Policy_on_rejects_an_unsigned_profile_file()
    {
        string path = Path.Combine(Path.GetTempPath(), "otw-apply-unsigned-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, ProfileFixtures.ValidProfile);
        try
        {
            var (root, stdout, stderr) = Build(engine: FakeApplyEngine.Create(new FakeMachine()).Engine, readers: ProfileFixtures.PolicyOn());

            int code = CliTestHost.Invoke(root, stdout, stderr, "apply", "--profile", path, "--what-if", "--json");

            Assert.Equal(ExitCodes.InvalidInput, code);
            Assert.Contains(ProfilePolicy.RequireSignedProfilesValueName, stderr.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Profile_restore_point_option_is_honoured()
    {
        // ValidProfile sets restorePoint:true; flip it off and confirm apply does not request one.
        string json = ProfileFixtures.ValidProfile.Replace("\"restorePoint\": true", "\"restorePoint\": false", StringComparison.Ordinal);
        string path = Path.Combine(Path.GetTempPath(), "otw-rp-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        try
        {
            var (root, stdout, stderr) = Build(engine: FakeApplyEngine.Create(new FakeMachine()).Engine);

            int code = CliTestHost.Invoke(root, stdout, stderr, "apply", "--profile", path, "--yes", "--json");

            Assert.True(code is ExitCodes.Success or ExitCodes.RebootRequired);
            using var document = JsonDocument.Parse(stdout.ToString());
            Assert.Equal("NotRequested", document.RootElement.GetProperty("restorePoint").GetProperty("status").GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Profile_allow_advanced_option_unblocks_advanced_entries()
    {
        OperatingSystemFacts facts = FakeOperatingSystemInfo.Windows11Pro24H2();
        string advId = CatalogLoader.LoadBuiltIn().Catalog!.Entries
            .First(e => e.Risk == RiskTier.Advanced && e.AppliesTo.Matches(facts)).Id.Value;
        // include forces the Advanced entry into the selection regardless of level; the profile's
        // allowAdvanced then decides whether apply plans it or blocks it.
        string included = ProfileFixtures.ValidProfile.Replace("\"include\": []", $"\"include\": [\"{advId}\"]", StringComparison.Ordinal);

        (string risk, _, string blockedNote) = WhatIfEntry(included, advId);
        Assert.Equal("Advanced", risk);
        Assert.Contains("allow-advanced", blockedNote, StringComparison.Ordinal);

        string allowed = included.Replace("\"allowAdvanced\": false", "\"allowAdvanced\": true", StringComparison.Ordinal);
        (_, _, string allowedNote) = WhatIfEntry(allowed, advId);
        Assert.DoesNotContain("allow-advanced", allowedNote, StringComparison.Ordinal);
    }

    /// <summary>Runs <c>apply --what-if --json</c> for a profile file and returns the (risk, result, note) of one planned entry.</summary>
    private static (string Risk, string Result, string Note) WhatIfEntry(string profileJson, string id)
    {
        string path = Path.Combine(Path.GetTempPath(), "otw-adv-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, profileJson);
        try
        {
            var (root, stdout, stderr) = Build(engine: FakeApplyEngine.Create(new FakeMachine()).Engine);
            CliTestHost.Invoke(root, stdout, stderr, "apply", "--profile", path, "--what-if", "--json");

            using var document = JsonDocument.Parse(stdout.ToString());
            JsonElement entries = document.RootElement.GetProperty("entries");
            for (int i = 0; i < entries.GetArrayLength(); i++)
            {
                JsonElement entry = entries[i];
                if (!string.Equals(entry.GetProperty("id").GetString(), id, StringComparison.Ordinal))
                {
                    continue;
                }

                JsonElement note = entry.GetProperty("note");
                return (
                    entry.GetProperty("risk").GetString() ?? string.Empty,
                    entry.GetProperty("result").GetString() ?? string.Empty,
                    note.ValueKind == JsonValueKind.String ? note.GetString() ?? string.Empty : string.Empty);
            }

            throw new InvalidOperationException("Entry '" + id + "' not found in the plan.");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
