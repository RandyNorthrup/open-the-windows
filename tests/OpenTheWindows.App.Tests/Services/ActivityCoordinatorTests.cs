using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Persistence;
using OpenTheWindows.Core.Privacy;
using OpenTheWindows.Core.Startup;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.App.Tests.Services;

/// <summary>The activity coordinator delegates to the startup, access and watch services over in-memory sources.</summary>
public sealed class ActivityCoordinatorTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeMachine _machine = new() { User = new InteractiveUser("S-1-5-21-1-2-3-1001", "tester", true) };
    private readonly FakeStartupSource _startup = new();
    private readonly FakeConsentStoreSource _consent = new();
    private readonly FakePersistenceSource _persistence = new();

    private ActivityCoordinator Build()
    {
        ApplyHarness harness = FakeApplyEngine.Create(_machine);
        return new ActivityCoordinator(
            () => FakeActivitySources.StartupApps(_startup, harness, At),
            () => FakeActivitySources.Access(_consent, _machine),
            () => FakeActivitySources.Watch(_persistence, new FakePersistenceSnapshotStore(), _machine, At));
    }

    [Fact]
    public async Task Startup_apps_list_and_toggle()
    {
        _startup.Entries.Add(new StartupEntry(StartupLocation.MachineRun, "Updater", "u.exe"));
        ActivityCoordinator coordinator = Build();

        StartupApp app = Assert.Single(await coordinator.ListStartupAsync());
        ApplyResult result = await coordinator.SetStartupEnabledAsync(app, enabled: false);

        Assert.Equal(RunState.Completed, result.State);
        Assert.False(Assert.Single(await coordinator.ListStartupAsync()).Enabled);
        await Assert.ThrowsAsync<ArgumentNullException>(() => coordinator.SetStartupEnabledAsync(null!, true));
    }

    [Fact]
    public async Task Access_reads_default_or_every_capability()
    {
        _consent.Records.Add(new ConsentStoreRecord("webcam", "Cam_x", false, true, At.ToFileTime(), 0));
        _consent.Records.Add(new ConsentStoreRecord("contacts", "People_x", false, true, At.ToFileTime(), 0));
        ActivityCoordinator coordinator = Build();

        Assert.Single(await coordinator.RecentAccessAsync(all: false));
        Assert.Equal(2, (await coordinator.RecentAccessAsync(all: true)).Count);
    }

    [Fact]
    public async Task The_watch_takes_a_baseline_and_compares()
    {
        _persistence.Items.Add(new PersistenceItem(PersistenceKind.Service, "A", "x"));
        ActivityCoordinator coordinator = Build();

        Assert.Null(await coordinator.CompareAsync());
        Assert.Single((await coordinator.TakeSnapshotAsync()).Items);
        Assert.Empty((await coordinator.CompareAsync())!.Changes);
    }

    [Fact]
    public void Rejects_null_factories()
    {
        Assert.Throws<ArgumentNullException>(() => new ActivityCoordinator(null!, () => null!, () => null!));
        Assert.Throws<ArgumentNullException>(() => new ActivityCoordinator(() => null!, null!, () => null!));
        Assert.Throws<ArgumentNullException>(() => new ActivityCoordinator(() => null!, () => null!, null!));
    }
}
