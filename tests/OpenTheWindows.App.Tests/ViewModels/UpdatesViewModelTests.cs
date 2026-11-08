using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Tests.Fakes;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Updates;

namespace OpenTheWindows.App.Tests.ViewModels;

/// <summary>
/// The Updates page: status load, gating until loaded, pause presets, extended-pause
/// warning, resume, the managed read-only mode, the feature-update hold, the repair,
/// per-update hide, and how each outcome is reported.
/// </summary>
public sealed class UpdatesViewModelTests
{
    private static readonly DateTimeOffset Until = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeUpdateCoordinator _coordinator = new();
    private readonly FakeDialogService _dialog = new();

    private UpdatesViewModel Build() => new(_coordinator, _dialog);

    private async Task<UpdatesViewModel> LoadedAsync()
    {
        UpdatesViewModel page = Build();
        await page.RefreshCommand.ExecuteAsync(null).ConfigureAwait(false);
        return page;
    }

    private static UpdateStatus Status(bool paused = false, bool warning = false, DateTimeOffset? until = null, string? held = null)
        => new("24H2", "Professional", EndOfServicingEditionFamily.Consumer, new DateOnly(2026, 10, 13), 56, warning, paused, until, held, null, false);

    [Fact]
    public void Is_the_updates_page()
    {
        UpdatesViewModel page = Build();

        Assert.Equal(PageKeys.Updates, page.Key);
        Assert.False(string.IsNullOrEmpty(page.Title));
    }

    [Fact]
    public async Task Refresh_loads_the_status()
    {
        _coordinator.StatusToReturn = Status();

        UpdatesViewModel page = await LoadedAsync();

        Assert.Contains("24H2", page.CurrentVersion, StringComparison.Ordinal);
        Assert.Contains("Not paused", page.PauseState, StringComparison.Ordinal);
        Assert.Contains("Not held", page.TargetRelease, StringComparison.Ordinal);
        Assert.True(page.IsLoaded);
        Assert.True(page.CanControl);
        Assert.Equal(_coordinator.VersionsToReturn, page.HoldVersions);
    }

    [Fact]
    public void The_status_is_not_read_until_the_user_refreshes_and_nothing_is_controllable_before()
    {
        _coordinator.StatusToReturn = Status();
        UpdatesViewModel page = Build();

        // No activation hook and nothing read on construction: the machine is probed only
        // when the user asks, and until then no control can act (managed state is unknown).
        Assert.Equal(0, _coordinator.StatusCount);
        Assert.DoesNotContain("24H2", page.CurrentVersion, StringComparison.Ordinal);
        Assert.False(page.CanControl);
        Assert.False(page.PauseCommand.CanExecute(7));
        Assert.False(page.ExtendedPauseCommand.CanExecute(null));
        Assert.True(page.CanRepair);
    }

    [Fact]
    public async Task A_status_fault_is_reported_and_leaves_the_page_unloaded()
    {
        _coordinator.ThrowOnStatus = true;

        UpdatesViewModel page = await LoadedAsync();

        Assert.False(page.IsLoaded);
        Assert.False(page.IsBusy);
        Assert.Contains("status failed", page.OperationStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_paused_status_enables_resume()
    {
        _coordinator.StatusToReturn = Status(paused: true, until: Until);

        UpdatesViewModel page = await LoadedAsync();

        Assert.True(page.IsPaused);
        Assert.True(page.CanResume);
        Assert.True(page.ResumeCommand.CanExecute(null));
    }

    [Fact]
    public async Task Managed_updates_are_read_only()
    {
        _coordinator.StatusToReturn = Status(paused: true, until: Until, held: "24H2");
        _coordinator.ManagedToReturn = true;

        UpdatesViewModel page = await LoadedAsync();

        Assert.True(page.IsManaged);
        Assert.False(page.CanControl);
        Assert.False(page.PauseCommand.CanExecute(7));
        Assert.False(page.ResumeCommand.CanExecute(null));
        Assert.False(page.ClearHoldCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_preset_pauses_for_that_many_days_and_says_until_when()
    {
        UpdatesViewModel page = await LoadedAsync();

        await page.PauseCommand.ExecuteAsync(14);

        Assert.Equal(1, _coordinator.PauseCount);
        Assert.Equal(14, _coordinator.LastPauseDays);
        Assert.False(_coordinator.LastPauseExtended);
        Assert.Contains("paused until 2026-11-2", page.OperationStatus, StringComparison.Ordinal);
        Assert.Equal(2, _coordinator.StatusCount);
    }

    [Fact]
    public async Task A_pause_blocked_by_policy_says_so()
    {
        _coordinator.ApplyToReturn = TestResults.ApplyResult(RunState.Completed, TestResults.Outcome("updates.pause", ApplyState.Managed));
        UpdatesViewModel page = await LoadedAsync();

        await page.PauseCommand.ExecuteAsync(7);

        Assert.Contains("managed by your organisation", page.OperationStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_pause_says_nothing_changed_and_why()
    {
        _coordinator.ApplyToReturn = TestResults.ApplyResult(RunState.RolledBack, TestResults.Outcome("updates.pause", ApplyState.RolledBack, "access denied"));
        UpdatesViewModel page = await LoadedAsync();

        await page.PauseCommand.ExecuteAsync(7);

        Assert.Contains("Nothing was changed", page.OperationStatus, StringComparison.Ordinal);
        Assert.Contains("access denied", page.OperationStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_extended_pause_warns_and_pauses_when_confirmed()
    {
        _dialog.ConfirmResult = true;
        UpdatesViewModel page = await LoadedAsync();
        page.ExtendedDays = 90;

        await page.ExtendedPauseCommand.ExecuteAsync(null);

        Assert.Equal(1, _dialog.ConfirmCount);
        Assert.Equal(1, _coordinator.PauseCount);
        Assert.Equal(90, _coordinator.LastPauseDays);
        Assert.True(_coordinator.LastPauseExtended);
    }

    [Fact]
    public async Task An_extended_pause_aborts_when_declined()
    {
        _dialog.ConfirmResult = false;
        UpdatesViewModel page = await LoadedAsync();

        await page.ExtendedPauseCommand.ExecuteAsync(null);

        Assert.Equal(0, _coordinator.PauseCount);
    }

    [Fact]
    public async Task Resume_resumes_updates()
    {
        _coordinator.StatusToReturn = Status(paused: true, until: Until);
        UpdatesViewModel page = await LoadedAsync();

        await page.ResumeCommand.ExecuteAsync(null);

        Assert.Equal(1, _coordinator.ResumeCount);
        Assert.Equal("Updates resumed.", page.OperationStatus);
    }

    [Fact]
    public async Task Resuming_a_settings_app_pause_says_it_was_ended()
    {
        _coordinator.ResumeToReturn = new ResumeOutcome(true, null, null, true, TestResults.EmptyApplyResult());
        UpdatesViewModel page = await LoadedAsync();

        await page.ResumeCommand.ExecuteAsync(null);

        Assert.Contains("Settings app", page.OperationStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_resume_whose_revert_failed_points_to_history()
    {
        _coordinator.ResumeToReturn = new ResumeOutcome(true, Guid.NewGuid(), new RevertResult(Guid.Empty, Guid.Empty, RunState.Failed, []), false);
        UpdatesViewModel page = await LoadedAsync();

        await page.ResumeCommand.ExecuteAsync(null);

        Assert.Contains("History", page.OperationStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resuming_when_not_paused_says_so()
    {
        _coordinator.ResumeToReturn = new ResumeOutcome(false, null, null, false);
        UpdatesViewModel page = await LoadedAsync();

        await page.ResumeCommand.ExecuteAsync(null);

        Assert.Equal("Updates were not paused.", page.OperationStatus);
    }

    [Fact]
    public async Task The_end_of_servicing_warning_reflects_the_status()
    {
        _coordinator.StatusToReturn = Status(warning: true);

        UpdatesViewModel page = await LoadedAsync();

        Assert.True(page.EndOfServiceWarning);
    }

    [Fact]
    public async Task Holding_needs_a_release_and_a_confirmation()
    {
        UpdatesViewModel page = await LoadedAsync();
        Assert.False(page.HoldCommand.CanExecute(null));

        page.SelectedHoldVersion = "24H2";
        _dialog.ConfirmResult = false;
        await page.HoldCommand.ExecuteAsync(null);
        Assert.Null(_coordinator.LastHold);

        _dialog.ConfirmResult = true;
        await page.HoldCommand.ExecuteAsync(null);
        Assert.Equal("24H2", _coordinator.LastHold);
        Assert.Contains("held at 24H2", page.OperationStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_hold_in_place_can_be_cleared()
    {
        _coordinator.StatusToReturn = Status(held: "24H2");
        UpdatesViewModel page = await LoadedAsync();

        Assert.True(page.IsHeld);
        Assert.Contains("Held at 24H2", page.TargetRelease, StringComparison.Ordinal);
        await page.ClearHoldCommand.ExecuteAsync(null);

        Assert.Equal(1, _coordinator.ClearHoldCount);
        Assert.Contains("was cleared", page.OperationStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Clearing_with_nothing_held_or_a_failed_revert_is_reported()
    {
        UpdatesViewModel page = await LoadedAsync();

        _coordinator.ClearToReturn = [];
        await page.ClearHoldCommand.ExecuteAsync(null);
        Assert.Contains("no hold", page.OperationStatus, StringComparison.Ordinal);

        _coordinator.ClearToReturn = [new RevertResult(Guid.Empty, Guid.Empty, RunState.Failed, [])];
        await page.ClearHoldCommand.ExecuteAsync(null);
        Assert.Contains("did not complete cleanly", page.OperationStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Repair_asks_first_then_reports_what_was_set_aside()
    {
        UpdatesViewModel page = await LoadedAsync();

        _dialog.ConfirmResult = false;
        await page.RepairCommand.ExecuteAsync(null);
        Assert.Equal(0, _coordinator.RepairCount);

        _dialog.ConfirmResult = true;
        await page.RepairCommand.ExecuteAsync(null);
        Assert.Equal(1, _coordinator.RepairCount);
        Assert.Contains("SoftwareDistribution.otw-1", page.OperationStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_repair_with_nothing_to_set_aside_or_a_failure_is_reported()
    {
        _dialog.ConfirmResult = true;
        UpdatesViewModel page = await LoadedAsync();

        _coordinator.RepairToReturn = new(true, ["Restarted."], [], null);
        await page.RepairCommand.ExecuteAsync(null);
        Assert.Contains("nothing to set aside", page.OperationStatus, StringComparison.Ordinal);

        _coordinator.RepairToReturn = new(false, [], [], "BITS would not stop");
        await page.RepairCommand.ExecuteAsync(null);
        Assert.Contains("BITS would not stop", page.OperationStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_null_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new UpdatesViewModel(null!, _dialog));
        Assert.Throws<ArgumentNullException>(() => new UpdatesViewModel(_coordinator, null!));
    }

    [Fact]
    public async Task Hide_sends_the_typed_kb_and_refreshes_the_hidden_list()
    {
        _coordinator.HiddenToReturn = [new DiscoveredUpdate("id", "Some update", ["KB5063878"], true)];
        UpdatesViewModel page = await LoadedAsync();
        page.KnowledgeBaseId = "KB5063878";

        await page.HideCommand.ExecuteAsync(null);

        Assert.Equal("KB5063878", _coordinator.LastHide);
        Assert.Contains("Hid", page.HideStatus, StringComparison.Ordinal);
        Assert.Contains("KB5063878  Some update", page.HiddenList, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unhide_sends_the_typed_kb()
    {
        UpdatesViewModel page = await LoadedAsync();
        page.KnowledgeBaseId = "KB5063878";

        await page.UnhideCommand.ExecuteAsync(null);

        Assert.Equal("KB5063878", _coordinator.LastUnhide);
        Assert.Contains("No hidden updates", page.HiddenList, StringComparison.Ordinal);
    }
}
