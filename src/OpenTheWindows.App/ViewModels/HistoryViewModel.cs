using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Resources;
using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// The History page: the prior runs (newest first), the entries of the selected run, and
/// revert — of a whole run, of one entry, or of everything the product ever changed. A
/// revert replays the captured prior state; the engine skips what an earlier revert
/// already undid and refuses when a later run changed the same settings. Runs load when
/// the page is shown, so nothing reads the journal until the user opens it.
/// </summary>
internal sealed partial class HistoryViewModel : ObservableObject, IPageViewModel, IActivatable
{
    private readonly IApplyCoordinator _coordinator;
    private readonly IDialogService _dialog;

    [ObservableProperty]
    private HistoryRunViewModel? _selectedRun;

    [ObservableProperty]
    private HistoryEntryViewModel? _selectedEntry;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _detailStatus = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Creates the page over the apply coordinator and the dialog service.</summary>
    public HistoryViewModel(IApplyCoordinator coordinator, IDialogService dialog)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(dialog);
        _coordinator = coordinator;
        _dialog = dialog;
    }

    /// <inheritdoc />
    public string Key => PageKeys.History;

    /// <inheritdoc />
    public string Title => Strings.Nav_History;

    /// <summary>The prior runs, newest first.</summary>
    public ObservableCollection<HistoryRunViewModel> Runs { get; } = [];

    /// <summary>The entries of the selected run (loaded when a run is selected).</summary>
    public ObservableCollection<HistoryEntryViewModel> Entries { get; } = [];

    /// <summary>Whether the selected run can be reverted right now.</summary>
    public bool CanRevert => SelectedRun?.CanRevert == true && !IsBusy;

    /// <summary>Whether the selected entry of the selected run can be reverted on its own right now.</summary>
    public bool CanRevertEntry => CanRevert && SelectedEntry?.CanRevert == true;

    /// <summary>Whether there is any run that Undo everything could revert.</summary>
    public bool CanUndoEverything => !IsBusy && Runs.Any(r => r.CanRevert);

    /// <inheritdoc />
    public void OnActivated() => Refresh();

    /// <summary>Reloads the run list from the journal.</summary>
    [RelayCommand]
    private void Refresh()
    {
        Runs.Clear();
        Entries.Clear();
        try
        {
            IReadOnlyList<JournalSummary> history = _coordinator.History();
            HashSet<Guid> reverted = [.. history
                .Where(s => s.RevertOf is not null && s.Result == RunState.Completed)
                .Select(s => s.RevertOf!.Value)];
            foreach (JournalSummary summary in history)
            {
                Runs.Add(new HistoryRunViewModel(summary, reverted.Contains(summary.RunId)));
            }

            Status = Runs.Count == 0
                ? "No runs yet."
                : string.Create(CultureInfo.CurrentCulture, $"{Runs.Count} run(s).");
        }
        catch (Exception ex) when (UiFaults.IsReportable(ex))
        {
            Status = string.Create(CultureInfo.CurrentCulture, $"Could not read the history: {ex.Message}");
        }

        NotifyRevertState();
    }

    /// <summary>Loads the selected run's entries from its journal (a newer selection supersedes a load in flight).</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LoadDetailsAsync(HistoryRunViewModel? run)
    {
        Entries.Clear();
        DetailStatus = string.Empty;
        if (run is null)
        {
            return;
        }

        try
        {
            JournalRecord record = await _coordinator.LoadRunAsync(run.RunId).ConfigureAwait(true);
            if (!ReferenceEquals(run, SelectedRun))
            {
                return;
            }

            foreach (JournalEntry entry in record.Entries)
            {
                Entries.Add(new HistoryEntryViewModel(entry));
            }

            DetailStatus = record.Entries.Count == 0 ? "This run changed nothing." : string.Empty;
        }
        catch (Exception ex) when (UiFaults.IsReportable(ex))
        {
            DetailStatus = string.Create(CultureInfo.CurrentCulture, $"Could not read this run's journal: {ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanRevert))]
    private async Task RevertAsync()
    {
        if (SelectedRun is not { } run || !_dialog.Confirm(Title,
            string.Create(CultureInfo.CurrentCulture, $"Revert the run from {run.StartedAtLabel}? This restores the values it changed.")))
        {
            return;
        }

        await RevertCoreAsync(run.RunId, null).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanRevertEntry))]
    private async Task RevertEntryAsync()
    {
        if (SelectedRun is not { } run || SelectedEntry is not { } entry || !_dialog.Confirm(Title,
            string.Create(CultureInfo.CurrentCulture, $"Revert only “{entry.Id}” from the run of {run.StartedAtLabel}? The rest of that run stays applied.")))
        {
            return;
        }

        await RevertCoreAsync(run.RunId, [entry.Id]).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanUndoEverything))]
    private async Task UndoEverythingAsync()
    {
        if (!_dialog.Confirm(Title,
            "Undo everything Open the Windows has changed on this PC? Every run that is not already reverted is reverted, newest first. Each revert is recorded, so it shows here afterwards."))
        {
            return;
        }

        IsBusy = true;
        try
        {
            IReadOnlyList<(JournalSummary Run, RevertResult Result)> results =
                await _coordinator.RevertEverythingAsync(new RevertOptions(false, true)).ConfigureAwait(true);
            _dialog.ShowMessage(Title, DescribeEverything(results));
        }
        catch (Exception ex) when (UiFaults.IsReportable(ex))
        {
            _dialog.ShowMessage(Title, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }

        Refresh();
    }

    private async Task RevertCoreAsync(Guid runId, IReadOnlyCollection<string>? entryIds)
    {
        IsBusy = true;
        try
        {
            RevertResult result = await _coordinator.RevertAsync(runId, new RevertOptions(false, true, EntryIds: entryIds)).ConfigureAwait(true);
            _dialog.ShowMessage(Title, Describe(result));
        }
        catch (Exception ex) when (UiFaults.IsReportable(ex))
        {
            _dialog.ShowMessage(Title, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }

        Refresh();
    }

    private static string DescribeEverything(IReadOnlyList<(JournalSummary Run, RevertResult Result)> results)
    {
        if (results.Count == 0)
        {
            return "There was nothing left to undo.";
        }

        int clean = results.Count(r => r.Result.State == RunState.Completed);
        string summary = string.Create(CultureInfo.CurrentCulture, $"Reverted {clean} of {results.Count} run(s).");
        return clean == results.Count ? summary : summary + " Select a run to see which entries did not revert.";
    }

    private static string Describe(RevertResult result)
    {
        if (result.AlreadyReverted)
        {
            return "That was already reverted; nothing was changed.";
        }

        if (result.State == RunState.Completed)
        {
            return "Reverted.";
        }

        string? reason = result.Entries.Select(e => e.Note).FirstOrDefault(n => !string.IsNullOrEmpty(n));
        return reason is null
            ? "The revert did not complete cleanly; see the history for details."
            : string.Create(CultureInfo.CurrentCulture, $"The revert did not go ahead: {reason}");
    }

    partial void OnSelectedRunChanged(HistoryRunViewModel? value)
    {
        SelectedEntry = null;
        NotifyRevertState();
        LoadDetailsCommand.Execute(value);
    }

    partial void OnSelectedEntryChanged(HistoryEntryViewModel? value) => NotifyRevertState();

    partial void OnIsBusyChanged(bool value) => NotifyRevertState();

    private void NotifyRevertState()
    {
        OnPropertyChanged(nameof(CanRevert));
        OnPropertyChanged(nameof(CanRevertEntry));
        OnPropertyChanged(nameof(CanUndoEverything));
        RevertCommand.NotifyCanExecuteChanged();
        RevertEntryCommand.NotifyCanExecuteChanged();
        UndoEverythingCommand.NotifyCanExecuteChanged();
    }
}
