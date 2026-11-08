using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Persistence;
using OpenTheWindows.Core.Privacy;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Core.Tests.Activity;

/// <summary>
/// The read-only app-access view (FILETIME decoding, filtering, desktop-app paths, in-use
/// first) and the persistence watch (baseline, added / removed / changed, case-insensitive
/// keys, duplicates tolerated).
/// </summary>
public sealed class AccessAndWatchTests
{
    private const string Sid = "S-1-5-21-1-2-3-1001";
    private static readonly DateTimeOffset At = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void File_times_decode_and_zero_or_out_of_range_is_absent()
    {
        Assert.Equal(At, CapabilityAccessService.FromFileTime(At.ToFileTime()));
        Assert.Null(CapabilityAccessService.FromFileTime(0));
        Assert.Null(CapabilityAccessService.FromFileTime(-5));
        Assert.Null(CapabilityAccessService.FromFileTime(long.MaxValue));
    }

    [Fact]
    public void Recent_use_is_filtered_newest_first_with_desktop_paths_restored()
    {
        var source = new FakeConsentStoreSource();
        source.Records.Add(new ConsentStoreRecord("webcam", "Camera_x", false, false, At.AddDays(-3).ToFileTime(), At.AddDays(-3).ToFileTime() + 1));
        source.Records.Add(new ConsentStoreRecord("Microphone", @"C:#Tools#rec.exe", true, true, At.AddDays(-1).ToFileTime(), At.AddDays(-1).ToFileTime() + 1));
        source.Records.Add(new ConsentStoreRecord("location", "Maps_x", false, true, 0, 0));
        source.Records.Add(new ConsentStoreRecord("contacts", "People_x", false, true, At.ToFileTime(), 1));
        var machine = new FakeMachine { User = new InteractiveUser(Sid, "tester", true) };

        IReadOnlyList<CapabilityUse> uses = FakeActivitySources.Access(source, machine).Recent(CapabilityAccessService.DefaultCapabilities);

        Assert.Equal(2, uses.Count);
        Assert.Equal(@"C:\Tools\rec.exe", uses[0].App);
        Assert.True(uses[0].IsDesktopApp);
        Assert.False(uses[0].InUse);
        Assert.Equal("Camera_x", uses[1].App);
        Assert.Equal(3, FakeActivitySources.Access(source, machine).Recent(null).Count);
    }

    [Fact]
    public void Without_a_signed_in_user_only_machine_records_are_read()
    {
        var source = new FakeConsentStoreSource();
        source.Records.Add(new ConsentStoreRecord("webcam", "Machine_x", false, false, At.ToFileTime(), 0));
        source.Records.Add(new ConsentStoreRecord("webcam", "User_x", false, true, At.ToFileTime(), 0));

        CapabilityUse only = Assert.Single(FakeActivitySources.Access(source, new FakeMachine()).Recent(null));

        Assert.Equal("Machine_x", only.App);
        Assert.True(only.InUse);
        Assert.Throws<ArgumentNullException>(() => new CapabilityAccessService(null!, new FakeMachine()));
        Assert.Throws<ArgumentNullException>(() => new CapabilityAccessService(source, null!));
    }

    [Fact]
    public void Compare_needs_a_baseline_and_then_reports_each_kind_of_change()
    {
        var source = new FakePersistenceSource();
        var store = new FakePersistenceSnapshotStore();
        source.Items.Add(new PersistenceItem(PersistenceKind.Service, "Spooler", "start=2"));
        source.Items.Add(new PersistenceItem(PersistenceKind.RunKey, @"HKLM\Run\Old", "old.exe"));
        PersistenceWatchService watch = FakeActivitySources.Watch(source, store, new FakeMachine(), At);

        Assert.Null(watch.Compare());
        PersistenceSnapshot baseline = watch.TakeSnapshot();
        Assert.Equal(At, baseline.TakenAt);
        Assert.Same(baseline, store.Saved);
        Assert.Empty(watch.Compare()!.Changes);

        source.Items[0] = new PersistenceItem(PersistenceKind.Service, "SPOOLER", "start=4");
        source.Items.RemoveAt(1);
        source.Items.Add(new PersistenceItem(PersistenceKind.ScheduledTask, @"\New", "x.exe"));
        IReadOnlyList<PersistenceChange> changes = watch.Compare()!.Changes;

        Assert.Equal([PersistenceChangeKind.Changed, PersistenceChangeKind.Added, PersistenceChangeKind.Removed], changes.Select(c => c.Change));
        Assert.Equal("start=2", changes[0].Before!.Detail);
        Assert.Equal("start=4", changes[0].Item.Detail);
        Assert.Equal(@"HKLM\Run\Old", changes[2].Item.Key);
    }

    [Fact]
    public void Diff_tolerates_duplicate_keys_and_keeps_kinds_apart()
    {
        PersistenceItem service = new(PersistenceKind.Service, "Same", "a");
        PersistenceItem task = new(PersistenceKind.ScheduledTask, "Same", "a");

        IReadOnlyList<PersistenceChange> changes = PersistenceWatchService.Diff([service, service with { Detail = "dup" }], [service, task]);

        PersistenceChange added = Assert.Single(changes);
        Assert.Equal(PersistenceKind.ScheduledTask, added.Item.Kind);
        Assert.Throws<ArgumentNullException>(() => PersistenceWatchService.Diff(null!, []));
        Assert.Throws<ArgumentNullException>(() => PersistenceWatchService.Diff([], null!));
    }

    [Fact]
    public void The_watch_rejects_null_dependencies()
    {
        var source = new FakePersistenceSource();
        var store = new FakePersistenceSnapshotStore();
        var machine = new FakeMachine();
        var time = new FixedTimeProvider(At);

        Assert.Throws<ArgumentNullException>(() => new PersistenceWatchService(null!, store, machine, time));
        Assert.Throws<ArgumentNullException>(() => new PersistenceWatchService(source, null!, machine, time));
        Assert.Throws<ArgumentNullException>(() => new PersistenceWatchService(source, store, null!, time));
        Assert.Throws<ArgumentNullException>(() => new PersistenceWatchService(source, store, machine, null!));
    }
}
