using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
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
/// The History page: the prior runs (newest first) and a revert action that
/// replays a run's captured prior state. Runs load when the page is shown, so
/// nothing reads the journal until the user opens it.
/// </summary>
internal sealed partial class HistoryViewModel : ObservableObject, IPageViewModel, IActivatable
{
    private readonly IApplyCoordinator _coordinator;
    private readonly IDialogService _dialog;

    [ObservableProperty]
    private HistoryRunViewModel? _selectedRun;

    [ObservableProperty]
    private string _status = string.Empty;

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

    /// <summary>Whether the selected run can be reverted right now.</summary>
    public bool CanRevert => SelectedRun?.CanRevert == true && !IsBusy;

    /// <inheritdoc />
    public void OnActivated() => Refresh();

    /// <summary>Reloads the run list from the journal.</summary>
    [RelayCommand]
    private void Refresh()
    {
        Runs.Clear();
        foreach (JournalSummary summary in _coordinator.History())
        {
            Runs.Add(new HistoryRunViewModel(summary));
        }

        Status = Runs.Count == 0
            ? "No runs yet."
            : string.Create(CultureInfo.CurrentCulture, $"{Runs.Count} run(s).");
    }

    [RelayCommand(CanExecute = nameof(CanRevert))]
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "UI boundary: a fault while reverting must be shown, not crash the window; the engine already " +
            "journals and rolls back operational failures. Recorded in PLAN.md §7.3.")]
    private async Task RevertAsync()
    {
        if (SelectedRun is null || !_dialog.Confirm(Title,
            string.Create(CultureInfo.CurrentCulture, $"Revert the run from {SelectedRun.StartedAtLabel}? This restores the values it changed.")))
        {
            return;
        }

        IsBusy = true;
        try
        {
            RevertResult result = await _coordinator.RevertAsync(SelectedRun.RunId, new RevertOptions(false, true)).ConfigureAwait(true);
            _dialog.ShowMessage(Title, result.State == RunState.Completed
                ? "The run was reverted."
                : "The revert did not complete cleanly; see the history for details.");
            Refresh();
        }
        catch (Exception ex)
        {
            _dialog.ShowMessage(Title, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedRunChanged(HistoryRunViewModel? value)
    {
        OnPropertyChanged(nameof(CanRevert));
        RevertCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRevert));
        RevertCommand.NotifyCanExecuteChanged();
    }
}
