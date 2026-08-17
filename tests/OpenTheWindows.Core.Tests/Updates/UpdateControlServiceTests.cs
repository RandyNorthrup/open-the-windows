using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Tests.Catalog;
using OpenTheWindows.Core.Updates;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Core.Tests.Updates;

/// <summary>
/// End-to-end tests of the update-control service over an in-memory machine: pause
/// writes the six values and is journaled/reversible, resume reverts the pause and
/// triggers a scan, an extended pause writes Event 4002, and status reports
/// version, end-of-servicing warning and pause state.
/// </summary>
public sealed class UpdateControlServiceTests
{
    private static string? ReadUx(FakeMachine machine, string name)
    {
        RegistryValueSnapshot snapshot = ((IRegistryReader)machine).Read(
            RegistryHive.LocalMachine, null, WindowsUpdateSettings.UxSettingsPath, name);
        return snapshot is { Exists: true, Data: { } data } ? data.GetString() : null;
    }

    [Fact]
    public void Pause_writes_the_six_values_and_completes()
    {
        var machine = new FakeMachine();
        (UpdateControlService service, ApplyHarness harness) = FakeUpdateControl.Create(machine);

        PauseOutcome outcome = service.Pause(7);

        Assert.Equal(RunState.Completed, outcome.Result.State);
        foreach (PauseRegistryValue value in outcome.Plan.Values)
        {
            Assert.Equal(value.Value, ReadUx(machine, value.Name));
        }

        Assert.Contains(harness.Engine.History(), h => string.Equals(h.Profile, UpdateControlService.PauseProfileName, StringComparison.Ordinal));
    }

    [Fact]
    public void Resume_reverts_the_pause_and_triggers_a_scan()
    {
        var machine = new FakeMachine();
        (UpdateControlService service, _) = FakeUpdateControl.Create(machine);
        service.Pause(7);

        ResumeOutcome outcome = service.Resume();

        Assert.True(outcome.WasPaused);
        Assert.True(outcome.ScanTriggered);
        Assert.Null(ReadUx(machine, WindowsUpdateSettings.PauseUpdatesExpiryTime));
        Assert.Contains(machine.Commands, c => string.Equals(c.Executable, UpdateControlService.ScanExecutable, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Resume_when_not_paused_reports_nothing_to_clear_but_still_scans()
    {
        var machine = new FakeMachine();
        (UpdateControlService service, _) = FakeUpdateControl.Create(machine);

        ResumeOutcome outcome = service.Resume();

        Assert.False(outcome.WasPaused);
        Assert.True(outcome.ScanTriggered);
    }

    [Fact]
    public void Extended_pause_writes_event_4002()
    {
        var machine = new FakeMachine();
        (UpdateControlService service, ApplyHarness harness) = FakeUpdateControl.Create(machine);

        PauseOutcome outcome = service.Pause(90, extended: true);

        Assert.True(outcome.Extended);
        Assert.Equal(RunState.Completed, outcome.Result.State);
        Assert.True(harness.Audit.Has(AuditEventId.ExtendedPause));
    }

    [Fact]
    public void Pause_beyond_the_cap_without_extended_throws()
    {
        (UpdateControlService service, _) = FakeUpdateControl.Create(new FakeMachine());

        Assert.Throws<ArgumentOutOfRangeException>(() => service.Pause(90));
    }

    [Fact]
    public void Status_reports_version_and_end_of_service_warning()
    {
        var machine = new FakeMachine();
        (UpdateControlService service, _) = FakeUpdateControl.Create(machine);

        UpdateStatus status = service.Status();

        Assert.Equal("24H2", status.CurrentVersion);
        Assert.Equal(EndOfServicingEditionFamily.Consumer, status.Family);
        Assert.Equal(57, status.DaysUntilEndOfService);
        Assert.True(status.CurrentVersionWithinWarning);
        Assert.False(status.IsPaused);
    }

    [Fact]
    public void Status_reports_the_pause_after_pausing()
    {
        var machine = new FakeMachine();
        (UpdateControlService service, _) = FakeUpdateControl.Create(machine);
        service.Pause(14);

        UpdateStatus status = service.Status();

        Assert.True(status.IsPaused);
        Assert.NotNull(status.PausedUntil);
    }

    [Fact]
    public void Status_reports_a_target_release_pin()
    {
        var machine = new FakeMachine();
        machine.SetRegistry(
            RegistryHive.LocalMachine, null, WindowsUpdateSettings.PolicyPath,
            WindowsUpdateSettings.TargetReleaseVersionInfo, RegistryValueType.Sz, CatalogTestData.Text("24H2"));
        (UpdateControlService service, _) = FakeUpdateControl.Create(machine);

        UpdateStatus status = service.Status();

        Assert.Equal("24H2", status.TargetReleaseVersion);
        Assert.True(status.TargetWithinWarning);
    }

    [Fact]
    public void Pause_what_if_writes_nothing()
    {
        var machine = new FakeMachine();
        (UpdateControlService service, _) = FakeUpdateControl.Create(machine);

        PauseOutcome outcome = service.Pause(7, whatIf: true);

        Assert.Equal(0, machine.WriteCount);
        Assert.Null(ReadUx(machine, WindowsUpdateSettings.PauseUpdatesExpiryTime));
        Assert.Equal(RunState.Completed, outcome.Result.State);
        Assert.Equal(6, outcome.Plan.Values.Count);
    }

    [Fact]
    public void Resume_what_if_does_not_revert_or_scan()
    {
        var machine = new FakeMachine();
        (UpdateControlService service, _) = FakeUpdateControl.Create(machine);
        service.Pause(7);

        ResumeOutcome outcome = service.Resume(whatIf: true);

        Assert.True(outcome.WasPaused);
        Assert.False(outcome.ScanTriggered);
        Assert.NotNull(ReadUx(machine, WindowsUpdateSettings.PauseUpdatesExpiryTime));
        Assert.Empty(machine.Commands);
    }

    [Fact]
    public void Status_ignores_a_blank_target_pin()
    {
        var machine = new FakeMachine();
        machine.SetRegistry(
            RegistryHive.LocalMachine, null, WindowsUpdateSettings.PolicyPath,
            WindowsUpdateSettings.TargetReleaseVersionInfo, RegistryValueType.Sz, CatalogTestData.Text("   "));
        (UpdateControlService service, _) = FakeUpdateControl.Create(machine);

        UpdateStatus status = service.Status();

        Assert.Null(status.TargetReleaseVersion);
    }

    [Fact]
    public void Status_defaults_an_unknown_edition_to_consumer_servicing()
    {
        var machine = new FakeMachine();
        var os = new OperatingSystemFacts("Windows 11 Pro", "MysteryEdition", "24H2", 26100, 4351, "x64", "Client");
        (UpdateControlService service, _) = FakeUpdateControl.Create(machine, os);

        UpdateStatus status = service.Status();

        Assert.Equal(EndOfServicingEditionFamily.Consumer, status.Family);
    }

    [Fact]
    public void Status_reports_unknown_end_of_service_for_an_uncatalogued_version()
    {
        var machine = new FakeMachine();
        var os = new OperatingSystemFacts("Windows 11 Pro", "Professional", "22H2", 22621, 1, "x64", "Client");
        (UpdateControlService service, _) = FakeUpdateControl.Create(machine, os);

        UpdateStatus status = service.Status();

        Assert.Null(status.DaysUntilEndOfService);
        Assert.Null(status.EndOfServiceDate);
        Assert.False(status.CurrentVersionWithinWarning);
    }
}
