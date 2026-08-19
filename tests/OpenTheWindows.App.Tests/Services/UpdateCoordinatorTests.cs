using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Updates;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.App.Tests.Services;

/// <summary>The update coordinator delegates status/pause/resume to the control service over a fake machine.</summary>
public sealed class UpdateCoordinatorTests
{
    private static UpdateCoordinator Build()
    {
        (UpdateControlService service, _) = FakeUpdateControl.Create(new FakeMachine());
        return new UpdateCoordinator(() => service);
    }

    [Fact]
    public void Reports_a_status_and_is_not_managed_on_an_unmanaged_machine()
    {
        UpdateCoordinator coordinator = Build();

        UpdateStatus status = coordinator.Status();

        Assert.False(string.IsNullOrEmpty(status.CurrentVersion));
        Assert.False(coordinator.IsManaged());
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
    public void Rejects_a_null_factory()
    {
        Assert.Throws<ArgumentNullException>(() => new UpdateCoordinator(null!));
    }
}
