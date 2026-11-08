using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Tests.Fakes;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Persistence;
using OpenTheWindows.Core.Privacy;
using OpenTheWindows.Core.Startup;

namespace OpenTheWindows.App.Tests.ViewModels;

/// <summary>
/// The Startup page (app list, on/off with confirmation, the persistence watch) and the App
/// access page (recent use, every capability, faults reported), plus their list items.
/// </summary>
public sealed class ActivityPagesTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeActivityCoordinator _coordinator = new();
    private readonly FakeDialogService _dialog = new();

    private StartupViewModel Startup()
    {
        var page = new StartupViewModel(_coordinator, _dialog);
        page.OnActivated();
        return page;
    }

    [Fact]
    public void The_startup_page_lists_the_apps_on_activation()
    {
        _coordinator.Apps.Add(new StartupApp(StartupLocation.MachineRun, "Updater", "u.exe", true));
        _coordinator.Apps.Add(new StartupApp(StartupLocation.UserStartupFolder, "Chat.lnk", @"C:\s\Chat.lnk", false));

        StartupViewModel page = Startup();

        Assert.Equal(PageKeys.Startup, page.Key);
        Assert.Equal(2, page.Apps.Count);
        Assert.Contains("1 turned off", page.Status, StringComparison.Ordinal);
        Assert.Equal("You (Startup folder)", page.Apps[1].LocationLabel);
        Assert.Equal("Chat.lnk, Off, You (Startup folder)", page.Apps[1].ToString());
    }

    [Fact]
    public async Task Turning_off_asks_first_and_turning_on_does_not()
    {
        _coordinator.Apps.Add(new StartupApp(StartupLocation.MachineRun, "Updater", "u.exe", true));
        _coordinator.Apps.Add(new StartupApp(StartupLocation.MachineRun, "Old", "o.exe", false));
        StartupViewModel page = Startup();

        page.SelectedApp = page.Apps[0];
        Assert.True(page.CanTurnOff);
        Assert.False(page.CanTurnOn);
        _dialog.ConfirmResult = false;
        await page.TurnOffCommand.ExecuteAsync(null);
        Assert.Null(_coordinator.LastToggle);

        _dialog.ConfirmResult = true;
        await page.TurnOffCommand.ExecuteAsync(null);
        Assert.Equal(("Updater", false), (_coordinator.LastToggle!.Value.App.Name, _coordinator.LastToggle.Value.Enabled));
        Assert.Contains("will no longer start", page.Status, StringComparison.Ordinal);

        page.SelectedApp = page.Apps[1];
        await page.TurnOnCommand.ExecuteAsync(null);
        Assert.True(_coordinator.LastToggle.Value.Enabled);
        Assert.Equal(2, _dialog.ConfirmCount);
    }

    [Fact]
    public async Task A_toggle_that_did_not_complete_or_faulted_is_reported()
    {
        _coordinator.Apps.Add(new StartupApp(StartupLocation.MachineRun, "Old", "o.exe", false));
        _coordinator.ToggleResult = TestResults.ApplyResult(RunState.Failed, TestResults.Outcome("startup.app.x", ApplyState.Failed, "access denied"));
        StartupViewModel page = Startup();
        page.SelectedApp = page.Apps[0];

        await page.TurnOnCommand.ExecuteAsync(null);
        Assert.Contains("Nothing was changed", page.Status, StringComparison.Ordinal);
        Assert.Contains("access denied", page.Status, StringComparison.Ordinal);

        _coordinator.Throw = true;
        await page.TurnOnCommand.ExecuteAsync(null);
        Assert.Contains("activity failed", page.Status, StringComparison.Ordinal);
        Assert.False(page.IsBusy);
    }

    [Fact]
    public async Task Nothing_selected_changes_nothing_and_an_empty_or_failed_list_says_so()
    {
        StartupViewModel page = Startup();
        Assert.Equal("No startup apps found.", page.Status);
        await page.TurnOnCommand.ExecuteAsync(null);
        Assert.Null(_coordinator.LastToggle);

        _coordinator.Throw = true;
        await page.RefreshCommand.ExecuteAsync(null);
        Assert.Contains("Could not read the startup apps", page.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_watch_records_a_baseline_after_confirmation_and_lists_changes()
    {
        StartupViewModel page = Startup();
        Assert.True(page.CanWatch);

        _dialog.ConfirmResult = false;
        await page.TakeSnapshotCommand.ExecuteAsync(null);
        Assert.Equal(0, _coordinator.SnapshotCount);
        _dialog.ConfirmResult = true;
        await page.TakeSnapshotCommand.ExecuteAsync(null);
        Assert.Contains("Recorded 1 autostarts", page.WatchStatus, StringComparison.Ordinal);

        await page.CompareCommand.ExecuteAsync(null);
        Assert.Contains("no baseline yet", page.WatchStatus, StringComparison.Ordinal);

        PersistenceSnapshot baseline = new(1, At, []);
        _coordinator.Comparison = new PersistenceComparison(baseline, []);
        await page.CompareCommand.ExecuteAsync(null);
        Assert.Contains("Nothing changed", page.WatchStatus, StringComparison.Ordinal);

        _coordinator.Comparison = new PersistenceComparison(baseline,
        [
            new PersistenceChange(null, new PersistenceItem(PersistenceKind.ScheduledTask, @"\New", "n.exe")),
            new PersistenceChange(new PersistenceItem(PersistenceKind.RunKey, @"HKLM\Run\Old", "o.exe"), null),
            new PersistenceChange(new PersistenceItem(PersistenceKind.Service, "Svc", "start=2"), new PersistenceItem(PersistenceKind.Service, "Svc", "start=4")),
            new PersistenceChange(null, new PersistenceItem(PersistenceKind.StartupFolder, @"C:\s\x.lnk", "1 bytes")),
        ]);
        await page.CompareCommand.ExecuteAsync(null);
        Assert.Contains("4 change(s)", page.WatchStatus, StringComparison.Ordinal);
        Assert.Equal(["New", "Gone", "Changed", "New"], page.Changes.Select(c => c.ChangeLabel), StringComparer.Ordinal);
        Assert.Equal(["Scheduled task", "Run key", "Service", "Startup folder"], page.Changes.Select(c => c.KindLabel), StringComparer.Ordinal);
        Assert.Contains("was: start=2", page.Changes[2].Detail, StringComparison.Ordinal);
        Assert.Equal(@"New Scheduled task, \New", page.Changes[0].ToString());

        _coordinator.Throw = true;
        await page.CompareCommand.ExecuteAsync(null);
        Assert.Equal("activity failed", page.WatchStatus);
    }

    [Fact]
    public void The_access_page_lists_recent_use_and_rereads_for_every_capability()
    {
        _coordinator.Uses.Add(new CapabilityUse(CapabilityAccessService.Webcam, "Camera_x", false, true, At, null));
        _coordinator.Uses.Add(new CapabilityUse(CapabilityAccessService.Microphone, @"C:\rec.exe", true, true, At, At));
        _coordinator.Uses.Add(new CapabilityUse(CapabilityAccessService.Location, "Maps_x", false, false, At, At));
        _coordinator.Uses.Add(new CapabilityUse("contacts", "People_x", false, true, At, At));
        var page = new AccessViewModel(_coordinator);

        page.OnActivated();

        Assert.Equal(PageKeys.Access, page.Key);
        Assert.False(_coordinator.LastAll);
        Assert.Contains("4 app use(s) recorded, 1 in use now", page.Status, StringComparison.Ordinal);
        Assert.Equal(["Camera", "Microphone", "Location", "contacts"], page.Uses.Select(u => u.CapabilityLabel), StringComparer.Ordinal);
        Assert.Equal("In use now", page.Uses[0].WhenLabel);
        Assert.Equal("Desktop app", page.Uses[1].KindLabel);
        Assert.StartsWith("Camera, In use now", page.Uses[0].ToString(), StringComparison.Ordinal);

        page.ShowAll = true;
        Assert.True(_coordinator.LastAll);
    }

    [Fact]
    public void The_access_page_reports_no_use_and_faults()
    {
        var page = new AccessViewModel(_coordinator);
        page.OnActivated();
        Assert.Equal("No recorded use.", page.Status);

        _coordinator.Throw = true;
        page.OnActivated();
        Assert.Contains("Could not read the recorded use", page.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void Pages_and_items_reject_null_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new StartupViewModel(null!, _dialog));
        Assert.Throws<ArgumentNullException>(() => new StartupViewModel(_coordinator, null!));
        Assert.Throws<ArgumentNullException>(() => new AccessViewModel(null!));
        Assert.Throws<ArgumentNullException>(() => new StartupAppItemViewModel(null!));
        Assert.Throws<ArgumentNullException>(() => new PersistenceChangeViewModel(null!));
        Assert.Throws<ArgumentNullException>(() => new CapabilityUseViewModel(null!));
    }

    [Theory]
    [InlineData(StartupLocation.MachineRun, "All users (registry)")]
    [InlineData(StartupLocation.MachineRun32, "All users (registry, 32-bit)")]
    [InlineData(StartupLocation.UserRun, "You (registry)")]
    [InlineData(StartupLocation.MachineStartupFolder, "All users (Startup folder)")]
    [InlineData((StartupLocation)99, "99")]
    public void Startup_locations_read_in_plain_words(StartupLocation location, string label)
        => Assert.Equal(label, new StartupAppItemViewModel(new StartupApp(location, "a", "b", true)).LocationLabel);
}
