using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Startup;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Core.Tests.Activity;

/// <summary>
/// The startup switch: how a StartupApproved value is read and written, which key each
/// location uses, and that a toggle is a journaled, revertable engine run that never
/// touches the Run key itself.
/// </summary>
public sealed class StartupAppsTests
{
    private const string Sid = "S-1-5-21-1-2-3-1001";
    private static readonly DateTimeOffset At = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    private static RegistryValueSnapshot Binary(params byte[] bytes)
        => new(true, RegistryValueType.Binary, JsonSerializer.SerializeToElement(Convert.ToBase64String(bytes)));

    [Fact]
    public void A_missing_or_even_marker_value_is_on_and_an_odd_marker_is_off()
    {
        Assert.True(StartupApproved.IsEnabled(new RegistryValueSnapshot(false, null, null)));
        Assert.True(StartupApproved.IsEnabled(Binary()));
        Assert.True(StartupApproved.IsEnabled(Binary(0x02, 0, 0, 0)));
        Assert.True(StartupApproved.IsEnabled(Binary(0x06, 0, 0, 0)));
        Assert.False(StartupApproved.IsEnabled(Binary(0x03, 0, 0, 0)));
        Assert.False(StartupApproved.IsEnabled(Binary(0x07, 0, 0, 0)));
        Assert.True(StartupApproved.IsEnabled(new RegistryValueSnapshot(true, RegistryValueType.Dword, JsonSerializer.SerializeToElement(3))));
        Assert.Throws<ArgumentNullException>(() => StartupApproved.IsEnabled(null!));
    }

    [Fact]
    public void The_off_value_carries_the_time_and_the_on_value_is_zeroed()
    {
        byte[] off = StartupApproved.Value(false, At);
        byte[] on = StartupApproved.Value(true, At);

        Assert.Equal(12, off.Length);
        Assert.Equal(0x03, off[0]);
        Assert.Equal(At.ToFileTime(), BitConverter.ToInt64(off, 4));
        Assert.Equal([0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], on);
    }

    [Theory]
    [InlineData(StartupLocation.MachineRun, RegistryHive.LocalMachine, @"\Run")]
    [InlineData(StartupLocation.MachineRun32, RegistryHive.LocalMachine, @"\Run32")]
    [InlineData(StartupLocation.UserRun, RegistryHive.User, @"\Run")]
    [InlineData(StartupLocation.MachineStartupFolder, RegistryHive.LocalMachine, @"\StartupFolder")]
    [InlineData(StartupLocation.UserStartupFolder, RegistryHive.User, @"\StartupFolder")]
    public void Each_location_has_its_own_switch_key(StartupLocation location, RegistryHive hive, string suffix)
    {
        (RegistryHive actualHive, string path) = StartupApproved.KeyFor(location);

        Assert.Equal(hive, actualHive);
        Assert.Equal(StartupApproved.RootPath + suffix, path);
    }

    [Fact]
    public void An_unknown_location_is_rejected()
        => Assert.Throws<ArgumentOutOfRangeException>(() => StartupApproved.KeyFor((StartupLocation)99));

    [Fact]
    public void Ids_are_stable_valid_and_distinct_per_location_and_name()
    {
        var app = new StartupApp(StartupLocation.MachineRun, "Updater", "x", true);

        TweakId id = StartupAppsService.IdFor(app);

        Assert.Equal(id, StartupAppsService.IdFor(app with { Name = "UPDATER", Enabled = false }));
        Assert.NotEqual(id, StartupAppsService.IdFor(app with { Location = StartupLocation.UserRun }));
        Assert.StartsWith("startup.app.", id.Value, StringComparison.Ordinal);
        Assert.True(TweakId.IsValid(id.Value));
    }

    [Fact]
    public void Turning_an_app_off_writes_the_switch_and_reverting_removes_it_again()
    {
        var machine = new FakeMachine { User = new InteractiveUser(Sid, "tester", true) };
        var source = new FakeStartupSource();
        source.Entries.Add(new StartupEntry(StartupLocation.UserRun, "Chat", @"C:\Apps\chat.exe"));
        ApplyHarness harness = FakeApplyEngine.Create(machine);
        StartupAppsService service = FakeActivitySources.StartupApps(source, harness, At);

        (RegistryHive hive, string path) = StartupApproved.KeyFor(StartupLocation.UserRun);
        StartupApp chat = Assert.Single(service.List());
        ApplyResult off = service.SetEnabled(chat, enabled: false, whatIf: false);

        Assert.Equal(RunState.Completed, off.State);
        Assert.Equal(RestartRequirement.SignOut, off.Requires);
        Assert.False(Assert.Single(service.List()).Enabled);
        Assert.Equal(StartupApproved.Value(false, At), ((IRegistryReader)machine).Read(hive, Sid, path, "Chat").Data!.Value.GetBytesFromBase64());

        harness.Engine.Revert(off.RunId, new RevertOptions(false, false));
        Assert.True(Assert.Single(service.List()).Enabled);
        Assert.False(((IRegistryReader)machine).Read(hive, Sid, path, "Chat").Exists);
    }

    [Fact]
    public void Per_user_apps_are_not_listed_and_not_applied_without_a_signed_in_user()
    {
        var machine = new FakeMachine();
        var source = new FakeStartupSource();
        source.Entries.Add(new StartupEntry(StartupLocation.MachineRun, "Updater", "u.exe"));
        source.Entries.Add(new StartupEntry(StartupLocation.UserRun, "Chat", "c.exe"));
        StartupAppsService service = FakeActivitySources.StartupApps(source, FakeApplyEngine.Create(machine), At);

        StartupApp only = Assert.Single(service.List());
        ApplyResult result = service.SetEnabled(new StartupApp(StartupLocation.UserRun, "Chat", "c.exe", true), enabled: false, whatIf: false);

        Assert.Equal("Updater", only.Name);
        Assert.Equal(ApplyState.NotApplicable, Assert.Single(result.Entries).Result);
    }

    [Fact]
    public void The_service_rejects_null_arguments()
    {
        var machine = new FakeMachine();
        ApplyHarness harness = FakeApplyEngine.Create(machine);
        var source = new FakeStartupSource();
        var time = new FixedTimeProvider(At);

        Assert.Throws<ArgumentNullException>(() => new StartupAppsService(null!, machine, machine, harness.Engine, time));
        Assert.Throws<ArgumentNullException>(() => new StartupAppsService(source, null!, machine, harness.Engine, time));
        Assert.Throws<ArgumentNullException>(() => new StartupAppsService(source, machine, null!, harness.Engine, time));
        Assert.Throws<ArgumentNullException>(() => new StartupAppsService(source, machine, machine, null!, time));
        Assert.Throws<ArgumentNullException>(() => new StartupAppsService(source, machine, machine, harness.Engine, null!));
        Assert.Throws<ArgumentNullException>(() => FakeActivitySources.StartupApps(source, harness, At).SetEnabled(null!, true, false));
        Assert.Throws<ArgumentNullException>(() => StartupAppsService.IdFor(null!));
    }
}
