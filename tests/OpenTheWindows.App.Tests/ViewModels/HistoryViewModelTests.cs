using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Tests.Fakes;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.App.Tests.ViewModels;

/// <summary>
/// The History page: loading runs on activation, the selected run's entries, reverting a
/// whole run or one entry with confirmation, Undo everything, and how untrusted journals
/// and engine refusals are shown.
/// </summary>
public sealed class HistoryViewModelTests
{
    private readonly FakeApplyCoordinator _coordinator = new();
    private readonly FakeDialogService _dialog = new();

    private HistoryViewModel Build() => new(_coordinator, _dialog);

    private static JournalSummary Summary(RunState result = RunState.Completed, Guid? revertOf = null, JournalIntegrity integrity = JournalIntegrity.Verified)
        => new(Guid.NewGuid(), new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.Zero), "GUI Privacy", result, 2, revertOf, integrity);

    // A page showing one completed run whose journal has an applied and a skipped entry, with that run selected.
    private HistoryViewModel WithSelectedRun(out JournalSummary run)
    {
        run = Summary();
        _coordinator.HistoryToReturn = [run];
        _coordinator.Runs[run.RunId] = TestResults.Record(run.RunId, ("a.b.one", ApplyState.Applied), ("a.b.two", ApplyState.Skipped));
        HistoryViewModel page = Build();
        page.OnActivated();
        page.SelectedRun = page.Runs[0];
        return page;
    }

    [Fact]
    public void Is_the_history_page()
    {
        HistoryViewModel page = Build();

        Assert.Equal(PageKeys.History, page.Key);
        Assert.False(string.IsNullOrEmpty(page.Title));
    }

    [Fact]
    public void Activation_loads_the_runs()
    {
        _coordinator.HistoryToReturn = [Summary(), Summary()];
        HistoryViewModel page = Build();

        page.OnActivated();

        Assert.Equal(2, page.Runs.Count);
    }

    [Fact]
    public void An_empty_history_shows_a_message()
    {
        _coordinator.HistoryToReturn = [];
        HistoryViewModel page = Build();

        page.OnActivated();

        Assert.Empty(page.Runs);
        Assert.Contains("No runs", page.Status, StringComparison.Ordinal);
    }

    [Fact]
    public void A_completed_non_revert_run_can_be_reverted()
    {
        _coordinator.HistoryToReturn = [Summary(RunState.Completed)];
        HistoryViewModel page = Build();
        page.OnActivated();

        page.SelectedRun = page.Runs[0];

        Assert.True(page.CanRevert);
        Assert.True(page.RevertCommand.CanExecute(null));
    }

    [Fact]
    public void A_revert_run_cannot_be_reverted_again()
    {
        _coordinator.HistoryToReturn = [Summary(RunState.Completed, revertOf: Guid.NewGuid())];
        HistoryViewModel page = Build();
        page.OnActivated();

        page.SelectedRun = page.Runs[0];

        Assert.False(page.CanRevert);
    }

    [Fact]
    public async Task Revert_requires_confirmation()
    {
        _coordinator.HistoryToReturn = [Summary(RunState.Completed)];
        _dialog.ConfirmResult = false;
        HistoryViewModel page = Build();
        page.OnActivated();
        page.SelectedRun = page.Runs[0];

        await page.RevertCommand.ExecuteAsync(null);

        Assert.Equal(0, _coordinator.RevertCount);
    }

    [Fact]
    public async Task A_confirmed_revert_calls_the_coordinator_for_the_selected_run()
    {
        JournalSummary summary = Summary(RunState.Completed);
        _coordinator.HistoryToReturn = [summary];
        _dialog.ConfirmResult = true;
        HistoryViewModel page = Build();
        page.OnActivated();
        page.SelectedRun = page.Runs[0];

        await page.RevertCommand.ExecuteAsync(null);

        Assert.Equal(1, _coordinator.RevertCount);
        Assert.Equal(summary.RunId, _coordinator.LastRevertRunId);
        Assert.NotEmpty(_dialog.Messages);
    }

    [Fact]
    public void Selecting_a_run_loads_its_entries()
    {
        HistoryViewModel page = WithSelectedRun(out _);

        Assert.Equal(["a.b.one", "a.b.two"], page.Entries.Select(e => e.Id), StringComparer.Ordinal);
        Assert.True(page.Entries[0].CanRevert);
        Assert.False(page.Entries[1].CanRevert);
        Assert.Equal(string.Empty, page.DetailStatus);
    }

    [Fact]
    public void A_journal_that_cannot_be_loaded_is_reported_in_the_detail_pane()
    {
        _coordinator.ThrowOnLoadRun = true;
        _coordinator.HistoryToReturn = [Summary()];
        HistoryViewModel page = Build();
        page.OnActivated();

        page.SelectedRun = page.Runs[0];

        Assert.Empty(page.Entries);
        Assert.Contains("does not verify", page.DetailStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void A_run_that_changed_nothing_says_so()
    {
        JournalSummary run = Summary();
        _coordinator.HistoryToReturn = [run];
        _coordinator.Runs[run.RunId] = TestResults.Record(run.RunId);
        HistoryViewModel page = Build();
        page.OnActivated();

        page.SelectedRun = page.Runs[0];

        Assert.Contains("changed nothing", page.DetailStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task One_applied_entry_can_be_reverted_on_its_own()
    {
        HistoryViewModel page = WithSelectedRun(out JournalSummary run);
        _dialog.ConfirmResult = true;
        Assert.False(page.CanRevertEntry);

        page.SelectedEntry = page.Entries[0];
        Assert.True(page.CanRevertEntry);
        await page.RevertEntryCommand.ExecuteAsync(null);

        Assert.Equal(run.RunId, _coordinator.LastRevertRunId);
        Assert.Equal(["a.b.one"], _coordinator.LastRevertOptions!.EntryIds!);
        Assert.Contains("Reverted.", _dialog.Messages, StringComparer.Ordinal);
    }

    [Fact]
    public void A_skipped_entry_cannot_be_reverted_on_its_own()
    {
        HistoryViewModel page = WithSelectedRun(out _);

        page.SelectedEntry = page.Entries[1];

        Assert.False(page.CanRevertEntry);
        Assert.False(page.RevertEntryCommand.CanExecute(null));
    }

    [Fact]
    public async Task Declining_an_entry_revert_changes_nothing()
    {
        HistoryViewModel page = WithSelectedRun(out _);
        page.SelectedEntry = page.Entries[0];
        _dialog.ConfirmResult = false;

        await page.RevertEntryCommand.ExecuteAsync(null);

        Assert.Equal(0, _coordinator.RevertCount);
    }

    [Fact]
    public async Task An_already_reverted_run_is_reported_without_changing_anything()
    {
        HistoryViewModel page = WithSelectedRun(out _);
        _dialog.ConfirmResult = true;
        _coordinator.RevertResultToReturn = new RevertResult(Guid.Empty, Guid.Empty, RunState.Failed, [], AlreadyReverted: true);

        await page.RevertCommand.ExecuteAsync(null);

        Assert.Contains(_dialog.Messages, m => m.Contains("already reverted", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_refused_revert_shows_the_engine_reason()
    {
        HistoryViewModel page = WithSelectedRun(out _);
        _dialog.ConfirmResult = true;
        _coordinator.RevertResultToReturn = new RevertResult(Guid.Empty, Guid.Empty, RunState.Failed,
            [TestResults.Outcome("a.b.one", ApplyState.Refused, "a later run changed the same settings")]);

        await page.RevertCommand.ExecuteAsync(null);

        Assert.Contains(_dialog.Messages, m => m.Contains("a later run changed the same settings", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_failed_revert_without_a_reason_points_to_history_and_a_fault_is_shown()
    {
        HistoryViewModel page = WithSelectedRun(out _);
        _dialog.ConfirmResult = true;
        _coordinator.RevertResultToReturn = new RevertResult(Guid.Empty, Guid.Empty, RunState.Failed, []);

        await page.RevertCommand.ExecuteAsync(null);
        _coordinator.ThrowOnRevert = true;
        page.SelectedRun = page.Runs[0];
        await page.RevertCommand.ExecuteAsync(null);

        Assert.Contains(_dialog.Messages, m => m.Contains("did not complete cleanly", StringComparison.Ordinal));
        Assert.Contains("revert failed", _dialog.Messages, StringComparer.Ordinal);
        Assert.False(page.IsBusy);
    }

    [Fact]
    public void A_reverted_run_is_marked_and_a_tampered_run_cannot_be_reverted()
    {
        JournalSummary original = Summary();
        JournalSummary revert = Summary(revertOf: original.RunId);
        JournalSummary tampered = Summary(integrity: JournalIntegrity.Tampered);
        _coordinator.HistoryToReturn = [revert, original, tampered];
        HistoryViewModel page = Build();

        page.OnActivated();

        Assert.True(page.Runs[1].IsReverted);
        Assert.False(page.Runs[0].IsReverted);
        Assert.False(page.Runs[2].CanRevert);
        Assert.True(page.Runs[2].IsUntrusted);
        Assert.Contains("verify", page.Runs[2].ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Undo_everything_asks_first_then_reports_how_many_runs_reverted()
    {
        JournalSummary run = Summary();
        _coordinator.HistoryToReturn = [run];
        HistoryViewModel page = Build();
        page.OnActivated();
        Assert.True(page.CanUndoEverything);

        _dialog.ConfirmResult = false;
        await page.UndoEverythingCommand.ExecuteAsync(null);
        Assert.Equal(0, _coordinator.RevertEverythingCount);

        _dialog.ConfirmResult = true;
        _coordinator.EverythingToReturn =
        [
            (run, new RevertResult(Guid.NewGuid(), run.RunId, RunState.Completed, [])),
            (Summary(), new RevertResult(Guid.Empty, Guid.NewGuid(), RunState.Failed, [])),
        ];
        await page.UndoEverythingCommand.ExecuteAsync(null);

        Assert.Equal(1, _coordinator.RevertEverythingCount);
        Assert.Contains(_dialog.Messages, m => m.Contains("Reverted 1 of 2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Undo_everything_with_nothing_left_or_a_fault_is_reported()
    {
        _coordinator.HistoryToReturn = [Summary()];
        _dialog.ConfirmResult = true;
        HistoryViewModel page = Build();
        page.OnActivated();

        await page.UndoEverythingCommand.ExecuteAsync(null);
        _coordinator.ThrowOnRevert = true;
        await page.UndoEverythingCommand.ExecuteAsync(null);

        Assert.Contains("There was nothing left to undo.", _dialog.Messages, StringComparer.Ordinal);
        Assert.Contains("revert failed", _dialog.Messages, StringComparer.Ordinal);
    }

    [Fact]
    public void Undo_everything_is_unavailable_with_no_revertable_run()
    {
        _coordinator.HistoryToReturn = [Summary(RunState.RolledBack)];
        HistoryViewModel page = Build();

        page.OnActivated();

        Assert.False(page.CanUndoEverything);
    }

    [Fact]
    public void Rejects_null_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new HistoryViewModel(null!, _dialog));
        Assert.Throws<ArgumentNullException>(() => new HistoryViewModel(_coordinator, null!));
    }
}
