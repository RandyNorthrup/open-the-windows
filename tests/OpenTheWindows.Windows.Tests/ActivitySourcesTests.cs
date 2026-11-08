using System.Security.Principal;
using System.Text;
using OpenTheWindows.Core.Persistence;
using OpenTheWindows.Core.Privacy;
using OpenTheWindows.Core.Startup;
using OpenTheWindows.TestSupport.Fakes;
using OpenTheWindows.Windows.Readers;
using OpenTheWindows.Windows.Security;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// The startup, ConsentStore and persistence sources read the real machine (read-only, no
/// elevation needed; this account's own hive stands in for the signed-in user), and the
/// persistence baseline round-trips through a temporary folder and is ignored when it
/// cannot be trusted.
/// </summary>
public sealed class ActivitySourcesTests : IDisposable
{
    private static readonly string MySid = WindowsIdentity.GetCurrent().User!.Value;
    private static readonly DateTimeOffset At = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Directory.CreateTempSubdirectory("otw-activity").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Startup_folder_items_are_listed_without_desktop_ini_and_user_entries_need_a_sid()
    {
        File.WriteAllText(Path.Combine(_folder, "Tool.lnk"), "x");
        File.WriteAllText(Path.Combine(_folder, "desktop.ini"), "x");
        var source = new WindowsStartupSource(_folder);

        IReadOnlyList<StartupEntry> machine = source.List(null);
        IReadOnlyList<StartupEntry> withUser = source.List(MySid);

        StartupEntry tool = Assert.Single(machine, e => e.Location == StartupLocation.MachineStartupFolder);
        Assert.Equal("Tool.lnk", tool.Name);
        Assert.Equal(Path.Combine(_folder, "Tool.lnk"), tool.Command);
        Assert.DoesNotContain(machine, e => e.Location is StartupLocation.UserRun or StartupLocation.UserStartupFolder);
        Assert.True(withUser.Count >= machine.Count);
        Assert.DoesNotContain(new WindowsStartupSource(Path.Combine(_folder, "missing")).List(null), e => e.Location == StartupLocation.MachineStartupFolder);
    }

    [Fact]
    public void The_users_startup_folder_comes_from_their_own_hive()
    {
        string? folder = WindowsStartupSource.UserStartupFolder(MySid);

        Assert.True(folder is null || folder.EndsWith("Startup", StringComparison.OrdinalIgnoreCase));
        Assert.Null(WindowsStartupSource.UserStartupFolder("S-1-5-21-0-0-0-424242"));
    }

    [Fact]
    public void Consent_store_records_carry_a_capability_and_an_app()
    {
        IReadOnlyList<ConsentStoreRecord> records = new WindowsConsentStoreSource().List(MySid);

        Assert.All(records, r =>
        {
            Assert.False(string.IsNullOrEmpty(r.Capability));
            Assert.False(string.IsNullOrEmpty(r.KeyName));
            Assert.True(r.LastUsedTimeStart >= 0);
        });
        Assert.DoesNotContain(new WindowsConsentStoreSource().List(null), r => r.PerUser);
    }

    [Fact]
    public void Services_include_the_event_log_and_leave_out_per_session_instances()
    {
        List<PersistenceItem> services = [.. WindowsPersistenceSource.Services()];

        PersistenceItem eventLog = Assert.Single(services, s => string.Equals(s.Key, "EventLog", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("start=", eventLog.Detail, StringComparison.Ordinal);
        Assert.Contains("image=", eventLog.Detail, StringComparison.Ordinal);
        Assert.Contains(services, s => s.Detail.Contains("; dll=", StringComparison.Ordinal));
        Assert.DoesNotContain(services, s => s.Key.Contains("UserSvc_", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Tasks_are_listed_by_path_with_their_actions()
    {
        List<PersistenceItem> tasks = WindowsPersistenceSource.Tasks();

        Assert.NotEmpty(tasks);
        Assert.All(tasks, t =>
        {
            Assert.StartsWith(@"\", t.Key, StringComparison.Ordinal);
            Assert.StartsWith("enabled=", t.Detail, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void A_capture_covers_every_kind_including_startup_files()
    {
        var startup = new FakeStartupSource();
        string file = Path.Combine(_folder, "Tool.lnk");
        File.WriteAllText(file, "abc");
        startup.Entries.Add(new StartupEntry(StartupLocation.MachineStartupFolder, "Tool.lnk", file));
        startup.Entries.Add(new StartupEntry(StartupLocation.MachineStartupFolder, "Gone.lnk", Path.Combine(_folder, "Gone.lnk")));
        startup.Entries.Add(new StartupEntry(StartupLocation.MachineRun, "NotAFile", "x.exe"));

        IReadOnlyList<PersistenceItem> items = new WindowsPersistenceSource(startup).Capture(MySid);

        Assert.Contains(items, i => i.Kind == PersistenceKind.Service);
        Assert.Contains(items, i => i.Kind == PersistenceKind.ScheduledTask);
        Assert.StartsWith("3 bytes, modified ", Assert.Single(items, i => string.Equals(i.Key, file, StringComparison.Ordinal)).Detail, StringComparison.Ordinal);
        Assert.Equal("missing", Assert.Single(items, i => i.Key.EndsWith("Gone.lnk", StringComparison.Ordinal)).Detail);
        Assert.DoesNotContain(items, i => string.Equals(i.Key, "x.exe", StringComparison.Ordinal));
        Assert.All(items.Where(i => i.Kind == PersistenceKind.RunKey), i => Assert.Matches(@"^HK(LM|CU)\\", i.Key));
    }

    [Fact]
    public void The_baseline_round_trips_and_an_untrusted_corrupt_or_other_schema_file_is_ignored()
    {
        var store = new PersistenceSnapshotStore(_folder, null, _ => true);
        Assert.Null(store.Load());

        var snapshot = new PersistenceSnapshot(PersistenceSnapshot.CurrentSchemaVersion, At, [new PersistenceItem(PersistenceKind.RunKey, @"HKLM\Run\A", "a.exe")]);
        store.Save(snapshot);
        PersistenceSnapshot loaded = store.Load()!;
        Assert.Equal(At, loaded.TakenAt);
        Assert.Equal(snapshot.Items, loaded.Items);
        Assert.False(File.Exists(Path.Combine(_folder, "persistence-baseline.json.tmp")));

        Assert.Null(new PersistenceSnapshotStore(_folder, null, _ => false).Load());

        string path = Path.Combine(_folder, "persistence-baseline.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99", StringComparison.Ordinal), Encoding.UTF8);
        Assert.Null(store.Load());
        File.WriteAllText(path, "{ not json", Encoding.UTF8);
        Assert.Null(store.Load());
    }

    [Fact]
    public void Saving_goes_through_the_folder_guard()
    {
        var guard = new FakeSecureDirectory();
        var store = new PersistenceSnapshotStore(Path.Combine(_folder, "state"), guard, _ => true);

        store.Save(new PersistenceSnapshot(PersistenceSnapshot.CurrentSchemaVersion, At, []));

        Assert.Contains(Path.Combine(_folder, "state"), guard.Prepared, StringComparer.OrdinalIgnoreCase);
        Assert.Throws<ArgumentNullException>(() => store.Save(null!));
        Assert.Throws<ArgumentException>(() => new PersistenceSnapshotStore(" ", null, _ => true));
        Assert.Throws<ArgumentNullException>(() => new PersistenceSnapshotStore(_folder, null, null!));
    }
}
