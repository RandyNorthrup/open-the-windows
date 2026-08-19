using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Tests.Fakes;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.App.Tests.ViewModels;

/// <summary>The History page: loading runs on activation and reverting a selected run with confirmation.</summary>
public sealed class HistoryViewModelTests
{
    private readonly FakeApplyCoordinator _coordinator = new();
    private readonly FakeDialogService _dialog = new();

    private HistoryViewModel Build() => new(_coordinator, _dialog);

    private static JournalSummary Summary(RunState result = RunState.Completed, Guid? revertOf = null) => new(Guid.NewGuid(), new DateTimeOffset(2026, 8, 18, 10, 0, 0, TimeSpan.Zero), "GUI Privacy", result, 2, revertOf);

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
    public void Rejects_null_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new HistoryViewModel(null!, _dialog));
        Assert.Throws<ArgumentNullException>(() => new HistoryViewModel(_coordinator, null!));
    }
}
