using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Persistence;
using OpenTheWindows.Core.Privacy;
using OpenTheWindows.Core.Startup;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

/// <summary>
/// <c>otw startup</c>, <c>otw access</c> and <c>otw watch</c> over in-memory sources: listing,
/// the journaled startup switch (exit codes for missing, ambiguous and unelevated), the
/// read-only access view, and the persistence baseline and diff (exit 1 on changes).
/// </summary>
public sealed class ActivityCommandTests
{
    private const string Sid = "S-1-5-21-1-2-3-1001";
    private static readonly DateTimeOffset At = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeMachine _machine = new() { User = new InteractiveUser(Sid, "tester", true) };
    private readonly FakeStartupSource _startup = new();

    private StartupAppsService Startup(out ApplyHarness harness)
    {
        harness = FakeApplyEngine.Create(_machine);
        return FakeActivitySources.StartupApps(_startup, harness, At);
    }

    private static byte[]? Switch(FakeMachine machine, StartupLocation location, string name)
    {
        (RegistryHive hive, string path) = StartupApproved.KeyFor(location);
        RegistryValueSnapshot value = ((IRegistryReader)machine).Read(hive, hive == RegistryHive.User ? Sid : null, path, name);
        return value.Data?.GetBytesFromBase64();
    }

    [Fact]
    public void Startup_list_shows_each_app_with_its_switch()
    {
        _startup.Entries.Add(new StartupEntry(StartupLocation.MachineRun, "Updater", @"C:\Apps\updater.exe /bg"));
        _startup.Entries.Add(new StartupEntry(StartupLocation.UserRun, "Chat", @"C:\Apps\chat.exe"));
        (RegistryHive hive, string path) = StartupApproved.KeyFor(StartupLocation.UserRun);
        _machine.SetRegistry(hive, Sid, path, "Chat", RegistryValueType.Binary,
            JsonSerializer.SerializeToElement(Convert.ToBase64String(StartupApproved.Value(false, At))));
        var (root, stdout, stderr) = TestCli.ActivityHost(startupApps: Startup(out _));

        int code = CliTestHost.Invoke(root, stdout, stderr, "startup", "list");

        Assert.Equal(ExitCodes.Success, code);
        string output = stdout.ToString();
        Assert.Contains(@"on   MachineRun            Updater  C:\Apps\updater.exe /bg", output, StringComparison.Ordinal);
        Assert.Contains("off  UserRun               Chat", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_list_json_and_an_empty_list()
    {
        var (root, stdout, stderr) = TestCli.ActivityHost(startupApps: Startup(out _));
        Assert.Equal(ExitCodes.Success, CliTestHost.Invoke(root, stdout, stderr, "startup", "list"));
        Assert.Contains("No startup apps found.", stdout.ToString(), StringComparison.Ordinal);

        _startup.Entries.Add(new StartupEntry(StartupLocation.MachineStartupFolder, "Tool.lnk", @"C:\ProgramData\Startup\Tool.lnk"));
        var (root2, stdout2, stderr2) = TestCli.ActivityHost(startupApps: Startup(out _));
        Assert.Equal(ExitCodes.Success, CliTestHost.Invoke(root2, stdout2, stderr2, "startup", "list", "--json"));
        using var document = JsonDocument.Parse(stdout2.ToString());
        Assert.Equal("MachineStartupFolder", document.RootElement[0].GetProperty("location").GetString());
        Assert.True(document.RootElement[0].GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void Disable_writes_the_switch_through_a_journaled_run_and_enable_turns_it_back_on()
    {
        _startup.Entries.Add(new StartupEntry(StartupLocation.MachineRun, "Updater", @"C:\Apps\updater.exe"));
        StartupAppsService startup = Startup(out ApplyHarness harness);
        var (root, stdout, stderr) = TestCli.ActivityHost(startupApps: startup);

        int disabled = CliTestHost.Invoke(root, stdout, stderr, "startup", "disable", "updater");

        Assert.Equal(ExitCodes.Success, disabled);
        Assert.Equal(StartupApproved.Value(false, At), Switch(_machine, StartupLocation.MachineRun, "Updater"));
        Assert.Equal(StartupAppsService.ProfileName, Assert.Single(harness.Engine.History()).Profile);

        var (root2, stdout2, stderr2) = TestCli.ActivityHost(startupApps: startup);
        Assert.Equal(ExitCodes.Success, CliTestHost.Invoke(root2, stdout2, stderr2, "startup", "enable", "Updater", "--json"));
        Assert.Equal(StartupApproved.Value(true, At), Switch(_machine, StartupLocation.MachineRun, "Updater"));
        using var document = JsonDocument.Parse(stdout2.ToString());
        Assert.Equal("Completed", document.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public void A_what_if_toggle_changes_nothing_and_needs_no_elevation()
    {
        _startup.Entries.Add(new StartupEntry(StartupLocation.MachineRun, "Updater", @"C:\Apps\updater.exe"));
        var (root, stdout, stderr) = TestCli.ActivityHost(elevated: false, startupApps: Startup(out ApplyHarness harness));

        int code = CliTestHost.Invoke(root, stdout, stderr, "startup", "disable", "Updater", "--what-if");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Null(Switch(_machine, StartupLocation.MachineRun, "Updater"));
        Assert.Empty(harness.Engine.History());
    }

    [Fact]
    public void A_toggle_without_elevation_is_refused()
    {
        _startup.Entries.Add(new StartupEntry(StartupLocation.MachineRun, "Updater", @"C:\Apps\updater.exe"));
        var (root, stdout, stderr) = TestCli.ActivityHost(elevated: false, startupApps: Startup(out _));

        Assert.Equal(ExitCodes.ElevationRequired, CliTestHost.Invoke(root, stdout, stderr, "startup", "disable", "Updater"));
    }

    [Fact]
    public void An_unknown_or_ambiguous_name_is_invalid_input_and_location_settles_it()
    {
        _startup.Entries.Add(new StartupEntry(StartupLocation.MachineRun, "Updater", @"C:\Apps\updater.exe"));
        _startup.Entries.Add(new StartupEntry(StartupLocation.UserRun, "Updater", @"C:\Users\t\updater.exe"));
        StartupAppsService startup = Startup(out _);

        var (root, stdout, stderr) = TestCli.ActivityHost(startupApps: startup);
        Assert.Equal(ExitCodes.InvalidInput, CliTestHost.Invoke(root, stdout, stderr, "startup", "disable", "Nope"));
        Assert.Contains("No startup app named 'Nope'", stderr.ToString(), StringComparison.Ordinal);

        var (root2, stdout2, stderr2) = TestCli.ActivityHost(startupApps: startup);
        Assert.Equal(ExitCodes.InvalidInput, CliTestHost.Invoke(root2, stdout2, stderr2, "startup", "disable", "Updater"));
        Assert.Contains("pass --location", stderr2.ToString(), StringComparison.Ordinal);

        var (root3, stdout3, stderr3) = TestCli.ActivityHost(startupApps: startup);
        Assert.Equal(ExitCodes.Success, CliTestHost.Invoke(root3, stdout3, stderr3, "startup", "disable", "Updater", "--location", "UserRun"));
        Assert.NotNull(Switch(_machine, StartupLocation.UserRun, "Updater"));
        Assert.Null(Switch(_machine, StartupLocation.MachineRun, "Updater"));

        var (root4, stdout4, stderr4) = TestCli.ActivityHost(startupApps: startup);
        Assert.Equal(ExitCodes.InvalidInput, CliTestHost.Invoke(root4, stdout4, stderr4, "startup", "enable", "Updater", "--location", "MachineStartupFolder"));
        Assert.Contains("at MachineStartupFolder", stderr4.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_and_access_refuse_an_unsupported_os()
    {
        var (root, stdout, stderr) = TestCli.ActivityHost(supported: false);

        Assert.Equal(ExitCodes.UnsupportedPlatform, CliTestHost.Invoke(root, stdout, stderr, "startup", "list"));
        Assert.Equal(ExitCodes.UnsupportedPlatform, CliTestHost.Invoke(root, stdout, stderr, "startup", "disable", "x"));
        Assert.Equal(ExitCodes.UnsupportedPlatform, CliTestHost.Invoke(root, stdout, stderr, "access"));
    }

    [Fact]
    public void Access_lists_camera_microphone_and_location_use_newest_first_and_all_with_the_flag()
    {
        var source = new FakeConsentStoreSource();
        long earlier = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero).ToFileTime();
        long later = new DateTimeOffset(2026, 11, 6, 8, 0, 0, TimeSpan.Zero).ToFileTime();
        source.Records.Add(new ConsentStoreRecord("webcam", "Microsoft.WindowsCamera_8wekyb3d8bbwe", false, true, earlier, earlier + 1));
        source.Records.Add(new ConsentStoreRecord("microphone", @"C:#Apps#chat.exe", true, true, later, 0));
        source.Records.Add(new ConsentStoreRecord("contacts", "Some.App_x", false, true, later, later + 1));
        CapabilityAccessService access = FakeActivitySources.Access(source, _machine);

        var (root, stdout, stderr) = TestCli.ActivityHost(capabilityAccess: access);
        Assert.Equal(ExitCodes.Success, CliTestHost.Invoke(root, stdout, stderr, "access"));
        string[] lines = stdout.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Contains(@"in use now", lines[0], StringComparison.Ordinal);
        Assert.Contains(@"C:\Apps\chat.exe", lines[0], StringComparison.Ordinal);
        Assert.Contains("Microsoft.WindowsCamera_8wekyb3d8bbwe", lines[1], StringComparison.Ordinal);

        var (root2, stdout2, stderr2) = TestCli.ActivityHost(capabilityAccess: access);
        Assert.Equal(ExitCodes.Success, CliTestHost.Invoke(root2, stdout2, stderr2, "access", "--all", "--json"));
        using var document = JsonDocument.Parse(stdout2.ToString());
        Assert.Equal(3, document.RootElement.GetArrayLength());
        Assert.True(document.RootElement[0].GetProperty("inUse").GetBoolean());
    }

    [Fact]
    public void Access_with_no_recorded_use_says_so()
    {
        var (root, stdout, stderr) = TestCli.ActivityHost();

        Assert.Equal(ExitCodes.Success, CliTestHost.Invoke(root, stdout, stderr, "access"));
        Assert.Contains("No recorded use.", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Watch_diff_needs_a_baseline_then_reports_changes_with_exit_1()
    {
        var source = new FakePersistenceSource();
        var store = new FakePersistenceSnapshotStore();
        source.Items.Add(new PersistenceItem(PersistenceKind.Service, "Spooler", "start=2"));
        source.Items.Add(new PersistenceItem(PersistenceKind.RunKey, @"HKLM\Run\Old", "old.exe"));
        PersistenceWatchService watch = FakeActivitySources.Watch(source, store, _machine, At);

        var (root, stdout, stderr) = TestCli.ActivityHost(persistenceWatch: watch);
        Assert.Equal(ExitCodes.Error, CliTestHost.Invoke(root, stdout, stderr, "watch", "diff"));
        Assert.Contains("No baseline yet", stderr.ToString(), StringComparison.Ordinal);

        var (root2, stdout2, stderr2) = TestCli.ActivityHost(persistenceWatch: watch);
        Assert.Equal(ExitCodes.Success, CliTestHost.Invoke(root2, stdout2, stderr2, "watch", "snapshot"));
        Assert.Contains("Recorded 2 autostarts", stdout2.ToString(), StringComparison.Ordinal);

        var (root3, stdout3, stderr3) = TestCli.ActivityHost(persistenceWatch: watch);
        Assert.Equal(ExitCodes.Success, CliTestHost.Invoke(root3, stdout3, stderr3, "watch", "diff"));
        Assert.Contains("0 change(s)", stdout3.ToString(), StringComparison.Ordinal);

        source.Items[0] = new PersistenceItem(PersistenceKind.Service, "Spooler", "start=4");
        source.Items.RemoveAt(1);
        source.Items.Add(new PersistenceItem(PersistenceKind.ScheduledTask, @"\Evil", "enabled=True; evil.exe"));
        var (root4, stdout4, stderr4) = TestCli.ActivityHost(persistenceWatch: watch);
        Assert.Equal(ExitCodes.Drift, CliTestHost.Invoke(root4, stdout4, stderr4, "watch", "diff"));
        string text = stdout4.ToString();
        Assert.Contains("3 change(s)", text, StringComparison.Ordinal);
        Assert.Contains("was: start=2", text, StringComparison.Ordinal);
        Assert.Contains("now: start=4", text, StringComparison.Ordinal);
        Assert.Contains(@"Removed  RunKey         HKLM\Run\Old", text, StringComparison.Ordinal);
        Assert.Contains(@"Added    ScheduledTask  \Evil", text, StringComparison.Ordinal);

        var (root5, stdout5, stderr5) = TestCli.ActivityHost(persistenceWatch: watch);
        Assert.Equal(ExitCodes.Drift, CliTestHost.Invoke(root5, stdout5, stderr5, "watch", "diff", "--json"));
        using var document = JsonDocument.Parse(stdout5.ToString());
        Assert.Equal(3, document.RootElement.GetProperty("changes").GetArrayLength());
    }

    [Fact]
    public void Watch_snapshot_json_and_elevation()
    {
        var (root, stdout, stderr) = TestCli.ActivityHost(elevated: false);
        Assert.Equal(ExitCodes.ElevationRequired, CliTestHost.Invoke(root, stdout, stderr, "watch", "snapshot"));
        Assert.Equal(ExitCodes.ElevationRequired, CliTestHost.Invoke(root, stdout, stderr, "watch", "diff"));

        var (root2, stdout2, stderr2) = TestCli.ActivityHost();
        Assert.Equal(ExitCodes.Success, CliTestHost.Invoke(root2, stdout2, stderr2, "watch", "snapshot", "--json"));
        using var document = JsonDocument.Parse(stdout2.ToString());
        Assert.Equal(0, document.RootElement.GetProperty("count").GetInt32());
    }
}
