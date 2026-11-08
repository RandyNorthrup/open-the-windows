using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Updates;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.App.Tests.Services;

/// <summary>The update coordinator delegates status, pause/resume, hide, the feature-update hold and repair to the services over a fake machine.</summary>
public sealed class UpdateCoordinatorTests
{
    private readonly FakeMachine _machine = new();
    private readonly FakeUpdateRepair _repair = new();

    private UpdateCoordinator Build()
    {
        (UpdateControlService service, _) = FakeUpdateControl.Create(_machine);
        return new UpdateCoordinator(() => service, () => new FakeUpdateHideService(), () => FakeUpdateControl.CreateTarget(_machine), () => _repair);
    }

    [Fact]
    public async Task Reports_a_status_and_is_not_managed_on_an_unmanaged_machine()
    {
        UpdateCoordinator coordinator = Build();

        UpdateStatus status = await coordinator.StatusAsync();

        Assert.False(string.IsNullOrEmpty(status.CurrentVersion));
        Assert.False(await coordinator.IsManagedAsync());
    }

    [Fact]
    public async Task Pause_then_resume_complete()
    {
        UpdateCoordinator coordinator = Build();

        PauseOutcome paused = await coordinator.PauseAsync(7, extended: false);
        ResumeOutcome resumed = await coordinator.ResumeAsync();

        Assert.Equal(7, paused.Plan.Days);
        Assert.True(resumed.WasPaused);
    }

    [Fact]
    public async Task Hold_at_a_known_release_then_clear_it()
    {
        UpdateCoordinator coordinator = Build();
        IReadOnlyList<string> versions = await coordinator.TargetVersionsAsync();

        ApplyResult held = await coordinator.HoldAtAsync(versions[^1]);
        IReadOnlyList<RevertResult> cleared = await coordinator.ClearHoldAsync();

        Assert.Equal(RunState.Completed, held.State);
        Assert.All(cleared, r => Assert.Equal(RunState.Completed, r.State));
        Assert.NotEmpty(cleared);
    }

    [Fact]
    public async Task Repair_runs_for_real_not_as_a_what_if()
    {
        UpdateRepairResult result = await Build().RepairAsync();

        Assert.True(result.Succeeded);
        Assert.Equal([false], _repair.Calls);
    }

    [Fact]
    public async Task Hide_listing_delegates_to_the_hide_service()
    {
        Assert.NotNull(await Build().ListHiddenAsync());
    }

    [Fact]
    public void Rejects_a_null_factory()
    {
        (UpdateControlService service, _) = FakeUpdateControl.Create(new FakeMachine());
        UpdateControlService Control() => service;
        static IUpdateHideService Hide() => new FakeUpdateHideService();
        UpdateTargetService Target() => FakeUpdateControl.CreateTarget(_machine);
        IUpdateRepair Repair() => _repair;

        Assert.Throws<ArgumentNullException>(() => new UpdateCoordinator(null!, Hide, Target, Repair));
        Assert.Throws<ArgumentNullException>(() => new UpdateCoordinator(Control, null!, Target, Repair));
        Assert.Throws<ArgumentNullException>(() => new UpdateCoordinator(Control, Hide, null!, Repair));
        Assert.Throws<ArgumentNullException>(() => new UpdateCoordinator(Control, Hide, Target, null!));
    }
}
