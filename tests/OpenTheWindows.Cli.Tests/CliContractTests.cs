using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Core.Profiles;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

/// <summary>
/// The CLI's process contract and the commands added in the hardening pass: usage errors
/// and crashes never masquerade as drift, non-interactive Breaking confirmation, update
/// status/target/repair, per-entry and whole-history revert, and profile capture.
/// </summary>
public sealed class CliContractTests
{
    private static DoctorService Doctor(bool home = false)
        => new(
            new FakeOperatingSystemInfo(home ? FakeOperatingSystemInfo.Windows11Home24H2() : FakeOperatingSystemInfo.Windows11Pro24H2()),
            new FakeElevationContext(isElevated: true));

    [Fact]
    public void A_usage_error_exits_with_invalid_input_not_drift()
    {
        var (root, stdout, stderr) = CliTestHost.DefaultHost();

        int code = CliTestHost.Invoke(root, stdout, stderr, "scan", "--no-such-option");

        Assert.Equal(ExitCodes.InvalidInput, code);
        Assert.Contains("--help", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void An_unexpected_failure_exits_with_error_not_drift()
    {
        var (root, stdout, stderr) = CliTestHost.Host(TestCli.Services(
            Doctor(), _ => CatalogLoader.LoadBuiltIn(), createApplyEngine: () => throw new InvalidOperationException("boom")));

        int code = CliTestHost.Invoke(root, stdout, stderr, "history");

        Assert.Equal(ExitCodes.Error, code);
        Assert.Contains("boom", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Root_help_lists_the_exit_codes()
    {
        var (root, stdout, stderr) = CliTestHost.DefaultHost();

        CliTestHost.Invoke(root, stdout, stderr, "--help");

        Assert.Contains("6 managed by policy", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_json_apply_with_breaking_entries_needs_yes_instead_of_prompting()
    {
        var (root, stdout, stderr) = TestCli.ApplyHost();

        int code = CliTestHost.Invoke(root, stdout, stderr, "apply", "--only", "security.crypto.fips-mode", "--allow-breaking", "--json", "--no-restore-point");

        Assert.Equal(ExitCodes.InvalidInput, code);
        Assert.Contains("--yes", stderr.ToString(), StringComparison.Ordinal);
        Assert.Empty(stdout.ToString());
    }

    [Fact]
    public void Update_status_has_a_json_form()
    {
        var (root, stdout, stderr) = CliTestHost.DefaultHost();

        int code = CliTestHost.Invoke(root, stdout, stderr, "updates", "status", "--json");

        Assert.Equal(ExitCodes.Success, code);
        using var document = JsonDocument.Parse(stdout.ToString());
        Assert.True(document.RootElement.TryGetProperty("currentVersion", out _));
    }

    [Fact]
    public void Update_target_lists_holds_and_validates_versions()
    {
        var machine = new FakeMachine();
        var (root, stdout, stderr) = CliTestHost.Host(TestCli.Services(
            Doctor(), _ => CatalogLoader.LoadBuiltIn(), createUpdateTarget: () => FakeUpdateControl.CreateTarget(machine)));

        Assert.Equal(ExitCodes.Success, CliTestHost.Invoke(root, stdout, stderr, "updates", "target", "--list"));
        Assert.Contains("24H2", stdout.ToString(), StringComparison.Ordinal);
        Assert.Equal(ExitCodes.Success, CliTestHost.Invoke(root, stdout, stderr, "updates", "target", "--version", "24h2"));
        Assert.Equal(ExitCodes.InvalidInput, CliTestHost.Invoke(root, stdout, stderr, "updates", "target", "--version", "19H1"));
        Assert.Equal(ExitCodes.InvalidInput, CliTestHost.Invoke(root, stdout, stderr, "updates", "target"));
        Assert.Equal(ExitCodes.Success, CliTestHost.Invoke(root, stdout, stderr, "updates", "target", "--clear"));
    }

    [Fact]
    public void Update_target_is_refused_on_windows_home()
    {
        var (root, stdout, stderr) = CliTestHost.Host(TestCli.Services(
            Doctor(home: true), _ => CatalogLoader.LoadBuiltIn(),
            createUpdateTarget: () => FakeUpdateControl.CreateTarget(new FakeMachine(), FakeOperatingSystemInfo.Windows11Home24H2())));

        int code = CliTestHost.Invoke(root, stdout, stderr, "updates", "target", "--version", "24H2");

        Assert.Equal(ExitCodes.InvalidInput, code);
        Assert.Contains("Home", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Update_repair_runs_the_repair_and_reports_its_steps()
    {
        var repair = new FakeUpdateRepair();
        var (root, stdout, stderr) = CliTestHost.Host(TestCli.Services(Doctor(), _ => CatalogLoader.LoadBuiltIn(), updateRepair: repair));

        int code = CliTestHost.Invoke(root, stdout, stderr, "updates", "repair", "--what-if");

        Assert.Equal(ExitCodes.Success, code);
        Assert.True(Assert.Single(repair.Calls));
        Assert.Contains("Stopped wuauserv.", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Revert_all_undoes_every_run_newest_first()
    {
        var time = new MutableTimeProvider(FakeApplyEngine.DefaultInstant);
        ApplyHarness harness = FakeApplyEngine.Create(new FakeMachine(), time: time);
        var (root, stdout, stderr) = TestCli.ApplyHost(engine: harness.Engine);
        CliTestHost.Invoke(root, stdout, stderr, "apply", "--only", "privacy.diagnostic-data.required-only", "--no-restore-point");
        time.Advance(TimeSpan.FromMinutes(1));
        CliTestHost.Invoke(root, stdout, stderr, "apply", "--only", "updates.insider.block-preview-builds", "--no-restore-point");
        time.Advance(TimeSpan.FromMinutes(1));

        int code = CliTestHost.Invoke(root, stdout, stderr, "revert", "--all");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("Every change this product made has been reverted.", stdout.ToString(), StringComparison.Ordinal);
        Assert.Equal(4, harness.Journal.Records.Count);
        Assert.Equal(ExitCodes.Success, CliTestHost.Invoke(root, stdout, stderr, "revert", "--all"));
    }

    [Fact]
    public void Profile_capture_writes_a_valid_profile()
    {
        string path = Path.Combine(Path.GetTempPath(), "otw-capture-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var (root, stdout, stderr) = CliTestHost.DefaultHost();

            int code = CliTestHost.Invoke(root, stdout, stderr, "profile", "capture", "--out", path, "--id", "lab-pc");

            Assert.Equal(ExitCodes.Success, code);
            ProfileLoadResult loaded = ProfileLoader.LoadText(path, File.ReadAllText(path));
            Assert.NotNull(loaded.Profile);
            Assert.Equal("lab-pc", loaded.Profile.Id);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Profile_capture_rejects_a_bad_id()
    {
        var (root, stdout, stderr) = CliTestHost.DefaultHost();

        int code = CliTestHost.Invoke(root, stdout, stderr, "profile", "capture", "--out", "x.json", "--id", "Bad Id");

        Assert.Equal(ExitCodes.InvalidInput, code);
    }
}
