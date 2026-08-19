using System.CommandLine;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

public sealed class RemediateCommandTests
{
    private static (RootCommand Root, StringWriter Out, StringWriter Err) Build(
        bool supported = true, bool elevated = true, ApplyEngine? engine = null, IJournalStore? journalStore = null)
        => TestCli.ApplyHost(supported, elevated, engine, journalStore: journalStore);

    /// <summary>A journal store whose only record captured the given OS build/UBR.</summary>
    private static FakeJournalStore JournalWithBuild(int build, int revision)
    {
        var store = new FakeJournalStore();
        store.Begin(new JournalRecord
        {
            RunId = Guid.NewGuid(),
            StartedAt = new DateTimeOffset(2026, 8, 17, 0, 0, 0, TimeSpan.Zero),
            AppVersion = "0.2.0",
            Os = new JournalOs(build, revision, "Professional"),
            Profile = "basic",
            Operator = @"TEST\tester",
            RestorePoint = new JournalRestorePoint(false, false, "NotRequested", null),
            Entries = [],
            Result = RunState.Completed,
        });
        return store;
    }

    [Fact]
    public void Unsupported_platform_exits_3()
    {
        var (root, stdout, stderr) = Build(supported: false);

        int code = CliTestHost.Invoke(root, stdout, stderr, "remediate", "--profile", "basic");

        Assert.Equal(ExitCodes.UnsupportedPlatform, code);
    }

    [Fact]
    public void Not_elevated_exits_5()
    {
        var (root, stdout, stderr) = Build(elevated: false);

        int code = CliTestHost.Invoke(root, stdout, stderr, "remediate", "--profile", "home");

        Assert.Equal(ExitCodes.ElevationRequired, code);
    }

    [Fact]
    public void Unknown_profile_exits_4()
    {
        var (root, stdout, stderr) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "remediate", "--profile", "nope");

        Assert.Equal(ExitCodes.InvalidInput, code);
    }

    [Fact]
    public void What_if_makes_no_changes()
    {
        var machine = new FakeMachine();
        ApplyHarness harness = FakeApplyEngine.Create(machine);
        var (root, stdout, stderr) = Build(engine: harness.Engine);

        int code = CliTestHost.Invoke(root, stdout, stderr, "remediate", "--profile", "home", "--include-draft", "--what-if");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Equal(0, machine.WriteCount);
    }

    [Fact]
    public void Applies_and_writes_report_to_out_creating_its_directory()
    {
        var (root, stdout, stderr) = Build(engine: FakeApplyEngine.Create(new FakeMachine()).Engine);
        string path = Path.Combine(Path.GetTempPath(), "otw-remediate-" + Guid.NewGuid().ToString("N"), "last-remediate.json");
        try
        {
            int code = CliTestHost.Invoke(
                root, stdout, stderr, "remediate", "--profile", "basic", "--include-draft", "--no-restore-point", "--out", path);

            Assert.True(code is ExitCodes.Success or ExitCodes.RebootRequired, "remediation should apply and succeed");
            Assert.True(File.Exists(path), "the report is written to --out, creating its directory");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal("Completed", document.RootElement.GetProperty("state").GetString());
            Assert.Contains("Wrote remediation report", stdout.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            string? dir = Path.GetDirectoryName(path);
            if (dir is not null && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void Json_report_goes_to_stdout_when_no_out()
    {
        var (root, stdout, stderr) = Build(engine: FakeApplyEngine.Create(new FakeMachine()).Engine);

        int code = CliTestHost.Invoke(root, stdout, stderr, "remediate", "--profile", "basic", "--include-draft", "--no-restore-point", "--json");

        Assert.True(code is ExitCodes.Success or ExitCodes.RebootRequired);
        using var document = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("Completed", document.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public void If_build_changed_skips_when_the_build_is_unchanged()
    {
        // The host reports Windows 11 24H2 (26100.4351); a journal at the same build means no feature update.
        var machine = new FakeMachine();
        var (root, stdout, stderr) = Build(engine: FakeApplyEngine.Create(machine).Engine, journalStore: JournalWithBuild(26100, 4351));

        int code = CliTestHost.Invoke(
            root, stdout, stderr, "remediate", "--profile", "basic", "--include-draft", "--no-restore-point", "--if-build-changed");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Equal(0, machine.WriteCount);
        Assert.Contains("unchanged", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void If_build_changed_applies_when_the_build_differs()
    {
        var (root, stdout, stderr) = Build(engine: FakeApplyEngine.Create(new FakeMachine()).Engine, journalStore: JournalWithBuild(26100, 4000));

        int code = CliTestHost.Invoke(
            root, stdout, stderr, "remediate", "--profile", "basic", "--include-draft", "--no-restore-point", "--if-build-changed", "--json");

        Assert.True(code is ExitCodes.Success or ExitCodes.RebootRequired);
        Assert.DoesNotContain("unchanged", stdout.ToString(), StringComparison.Ordinal);
        using var document = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("Completed", document.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public void User_scope_remediate_runs_unelevated_and_skips_the_logon_fallback()
    {
        // The Active Setup stub runs as the signing-in user (no admin token) and touches only
        // that user's own hive, so --user-scope must not be rejected for elevation and must not
        // itself try to register the machine-wide fallback.
        string path = WriteTempProfile(ProfileFixtures.AllUsersProfile());
        try
        {
            var registrar = new FakeActiveSetupRegistrar();
            var (root, stdout, stderr) = TestCli.ApplyHost(elevated: false, activeSetupRegistrar: registrar);

            int code = CliTestHost.Invoke(root, stdout, stderr, "remediate", "--profile", path, "--user-scope", "--json");

            Assert.NotEqual(ExitCodes.ElevationRequired, code);
            Assert.True(code is ExitCodes.Success or ExitCodes.RebootRequired);
            Assert.Empty(registrar.Registered);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void All_users_remediate_registers_the_per_user_logon_fallback()
    {
        string path = WriteTempProfile(ProfileFixtures.AllUsersProfile());
        try
        {
            var registrar = new FakeActiveSetupRegistrar();
            var (root, stdout, stderr) = TestCli.ApplyHost(activeSetupRegistrar: registrar);

            int code = CliTestHost.Invoke(root, stdout, stderr, "remediate", "--profile", path, "--no-restore-point", "--json");

            Assert.True(code is ExitCodes.Success or ExitCodes.RebootRequired);
            ActiveSetupComponent component = Assert.Single(registrar.Registered);
            Assert.Equal("{OTW-custom-all-users}", component.ComponentKey);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempProfile(string json)
    {
        string path = Path.Combine(Path.GetTempPath(), "otw-profile-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void If_build_changed_applies_when_there_is_no_prior_journal()
    {
        // No journal store record (default empty store) means no baseline, so the safe action is to apply.
        var (root, stdout, stderr) = Build(engine: FakeApplyEngine.Create(new FakeMachine()).Engine);

        int code = CliTestHost.Invoke(
            root, stdout, stderr, "remediate", "--profile", "basic", "--include-draft", "--no-restore-point", "--if-build-changed", "--json");

        Assert.True(code is ExitCodes.Success or ExitCodes.RebootRequired);
        using var document = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("Completed", document.RootElement.GetProperty("state").GetString());
    }
}
