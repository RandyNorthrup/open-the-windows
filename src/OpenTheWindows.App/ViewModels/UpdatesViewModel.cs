using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Resources;
using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Updates;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// The Updates page: the current release and its end-of-servicing, the pause state, quick
/// pause presets, an extended pause behind a warning, resume, holding feature updates at a
/// release, the supervised Windows Update repair and per-update hide. Reading the status
/// probes the machine, so it never runs on its own — the user loads it with the refresh
/// button, and every control stays disabled until it has (so a managed device is never
/// mistaken for an unmanaged one). When updates are managed by organisation policy the
/// controls are read-only. Every operation runs off the UI thread and reports its outcome.
/// </summary>
internal sealed partial class UpdatesViewModel : ObservableObject, IPageViewModel
{
    private const int SupportedMaxPauseDays = 35;
    private const string NotLoaded = "Not loaded — select “Refresh status”.";
    private const string ManagedMessage = "Windows Update is managed by your organisation; nothing was changed.";

    private readonly IUpdateCoordinator _coordinator;
    private readonly IDialogService _dialog;

    [ObservableProperty]
    private string _currentVersion = NotLoaded;

    [ObservableProperty]
    private string _endOfService = "—";

    [ObservableProperty]
    private bool _endOfServiceWarning;

    [ObservableProperty]
    private string _pauseState = "—";

    [ObservableProperty]
    private string _targetRelease = "—";

    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _isHeld;

    [ObservableProperty]
    private bool _isManaged;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _operationStatus = string.Empty;

    [ObservableProperty]
    private int _extendedDays = 90;

    [ObservableProperty]
    private string? _selectedHoldVersion;

    [ObservableProperty]
    private string _knowledgeBaseId = string.Empty;

    [ObservableProperty]
    private string _hiddenList = "Not loaded — select “Refresh hidden”.";

    [ObservableProperty]
    private string _hideStatus = string.Empty;

    /// <summary>Creates the page over the update coordinator and the dialog service.</summary>
    public UpdatesViewModel(IUpdateCoordinator coordinator, IDialogService dialog)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(dialog);
        _coordinator = coordinator;
        _dialog = dialog;
    }

    /// <inheritdoc />
    public string Key => PageKeys.Updates;

    /// <inheritdoc />
    public string Title => Strings.Nav_Updates;

    /// <summary>The supported pause presets (days), all within the Windows maximum.</summary>
    public IReadOnlyList<int> PausePresets { get; } = [7, 14, SupportedMaxPauseDays];

    /// <summary>The extended pause options (days) beyond the supported maximum.</summary>
    public IReadOnlyList<int> ExtendedOptions { get; } = [60, 90, 180, 365];

    /// <summary>The Windows 11 releases this device can hold feature updates at (loaded with the status).</summary>
    public ObservableCollection<string> HoldVersions { get; } = [];

    /// <summary>Whether the pause/resume/hold controls are usable (status loaded, not managed, not mid-operation).</summary>
    public bool CanControl => IsLoaded && !IsManaged && !IsBusy;

    /// <summary>Whether Resume is available (currently paused and controllable).</summary>
    public bool CanResume => IsPaused && CanControl;

    /// <summary>Whether hide/unhide can run (controllable and the KB looks valid).</summary>
    public bool CanHide => CanControl && Core.Updates.KnowledgeBaseId.Normalize(KnowledgeBaseId) is not null;

    /// <summary>Whether a feature-update hold can be set (controllable and a release is chosen).</summary>
    public bool CanHold => CanControl && SelectedHoldVersion is not null;

    /// <summary>Whether the feature-update hold can be cleared (controllable and a hold is set).</summary>
    public bool CanClearHold => CanControl && IsHeld;

    /// <summary>Whether the Windows Update repair can run (not mid-operation).</summary>
    public bool CanRepair => !IsBusy;

    /// <summary>Reads and shows the update status. User-initiated only.</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            await LoadStatusAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (UiFaults.IsReportable(ex))
        {
            OperationStatus = string.Create(CultureInfo.CurrentCulture, $"Could not read the update status: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadStatusAsync()
    {
        UpdateStatus status = await _coordinator.StatusAsync().ConfigureAwait(true);
        CurrentVersion = string.Create(CultureInfo.CurrentCulture, $"{status.CurrentVersion} ({status.EditionId})");
        EndOfService = status.EndOfServiceDate is { } date
            ? string.Create(CultureInfo.CurrentCulture, $"{date:yyyy-MM-dd} ({status.DaysUntilEndOfService} days)")
            : "Unknown";
        EndOfServiceWarning = status.CurrentVersionWithinWarning;
        IsPaused = status.IsPaused;
        PauseState = status is { IsPaused: true, PausedUntil: { } until }
            ? string.Create(CultureInfo.CurrentCulture, $"Paused until {until.LocalDateTime:yyyy-MM-dd}")
            : "Not paused";
        IsHeld = status.TargetReleaseVersion is not null;
        TargetRelease = status.TargetReleaseVersion is { } pin
            ? string.Create(CultureInfo.CurrentCulture, $"Held at {pin}")
            : "Not held";
        IsManaged = await _coordinator.IsManagedAsync().ConfigureAwait(true);
        if (HoldVersions.Count == 0)
        {
            foreach (string version in await _coordinator.TargetVersionsAsync().ConfigureAwait(true))
            {
                HoldVersions.Add(version);
            }
        }

        IsLoaded = true;
    }

    /// <summary>Pauses updates for a supported preset number of days.</summary>
    [RelayCommand(CanExecute = nameof(CanControl))]
    private async Task PauseAsync(int days)
    {
        await RunAsync(async () => DescribePause(await _coordinator.PauseAsync(days, extended: false).ConfigureAwait(true))).ConfigureAwait(true);
    }

    /// <summary>Pauses updates beyond the supported maximum, behind a warning.</summary>
    [RelayCommand(CanExecute = nameof(CanControl))]
    private async Task ExtendedPauseAsync()
    {
        string warning = string.Create(CultureInfo.CurrentCulture,
            $"Pausing updates for {ExtendedDays} days is beyond the Windows-supported maximum of {SupportedMaxPauseDays} days. You will not receive security updates for that whole time. Continue?");
        if (!_dialog.Confirm(Title, warning))
        {
            return;
        }

        int days = ExtendedDays;
        await RunAsync(async () => DescribePause(await _coordinator.PauseAsync(days, extended: true).ConfigureAwait(true))).ConfigureAwait(true);
    }

    /// <summary>Resumes updates now (also ending a pause set in the Settings app).</summary>
    [RelayCommand(CanExecute = nameof(CanResume))]
    private async Task ResumeAsync()
    {
        await RunAsync(async () => DescribeResume(await _coordinator.ResumeAsync().ConfigureAwait(true))).ConfigureAwait(true);
    }

    /// <summary>Holds feature updates at the chosen release, after a confirmation.</summary>
    [RelayCommand(CanExecute = nameof(CanHold))]
    private async Task HoldAsync()
    {
        if (SelectedHoldVersion is not { } version || !_dialog.Confirm(Title, string.Create(CultureInfo.CurrentCulture,
                $"Hold feature updates at Windows 11 {version}? Monthly quality and security updates keep arriving; only the move to a newer release waits. You can clear the hold at any time.")))
        {
            return;
        }

        await RunAsync(async () => DescribeRun(
            await _coordinator.HoldAtAsync(version).ConfigureAwait(true),
            string.Create(CultureInfo.CurrentCulture, $"Feature updates are held at {version}."))).ConfigureAwait(true);
    }

    /// <summary>Clears the feature-update hold.</summary>
    [RelayCommand(CanExecute = nameof(CanClearHold))]
    private async Task ClearHoldAsync()
    {
        await RunAsync(async () => DescribeClear(await _coordinator.ClearHoldAsync().ConfigureAwait(true))).ConfigureAwait(true);
    }

    /// <summary>Runs the supervised Windows Update repair, after a confirmation.</summary>
    [RelayCommand(CanExecute = nameof(CanRepair))]
    private async Task RepairAsync()
    {
        if (!_dialog.Confirm(Title,
                "Repair Windows Update? This stops the update services, sets the download cache and the catalogue database aside (renamed, not deleted) and starts the services again. Any download in progress starts over."))
        {
            return;
        }

        await RunAsync(async () => DescribeRepair(await _coordinator.RepairAsync().ConfigureAwait(true))).ConfigureAwait(true);
    }

    /// <summary>Reads the hidden-update list from the Windows Update Agent.</summary>
    [RelayCommand]
    private async Task RefreshHiddenAsync()
    {
        IsBusy = true;
        try
        {
            IReadOnlyList<DiscoveredUpdate> hidden = await _coordinator.ListHiddenAsync().ConfigureAwait(true);
            HiddenList = hidden.Count == 0
                ? "No hidden updates."
                : string.Join(Environment.NewLine, hidden.Select(u => string.Create(CultureInfo.CurrentCulture, $"{string.Join(", ", u.KnowledgeBaseIds)}  {u.Title}")));
        }
        catch (Exception ex) when (UiFaults.IsReportable(ex))
        {
            HiddenList = string.Create(CultureInfo.CurrentCulture, $"Could not read the hidden updates: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Hides the typed knowledge-base id.</summary>
    [RelayCommand(CanExecute = nameof(CanHide))]
    private async Task HideAsync()
        => await RunHideAsync(hide: true).ConfigureAwait(true);

    /// <summary>Unhides the typed knowledge-base id.</summary>
    [RelayCommand(CanExecute = nameof(CanHide))]
    private async Task UnhideAsync()
        => await RunHideAsync(hide: false).ConfigureAwait(true);

    private async Task RunHideAsync(bool hide)
    {
        IsBusy = true;
        UpdateHideOutcome? outcome = null;
        try
        {
            outcome = hide
                ? await _coordinator.HideAsync(KnowledgeBaseId).ConfigureAwait(true)
                : await _coordinator.UnhideAsync(KnowledgeBaseId).ConfigureAwait(true);
            HideStatus = outcome.Message;
        }
        catch (Exception ex) when (UiFaults.IsReportable(ex))
        {
            HideStatus = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }

        if (outcome?.Succeeded == true)
        {
            await RefreshHiddenAsync().ConfigureAwait(true);
        }
    }

    // Runs one change, shows its outcome, then re-reads the status so the page reflects it.
    private async Task RunAsync(Func<Task<string>> operation)
    {
        IsBusy = true;
        try
        {
            OperationStatus = await operation().ConfigureAwait(true);
            await LoadStatusAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (UiFaults.IsReportable(ex))
        {
            OperationStatus = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string DescribePause(PauseOutcome outcome)
        => DescribeRun(outcome.Result, string.Create(CultureInfo.CurrentCulture, $"Updates are paused until {outcome.Plan.Expiry.LocalDateTime:yyyy-MM-dd}."));

    private static string DescribeResume(ResumeOutcome outcome)
    {
        if (outcome.Revert is { State: not RunState.Completed })
        {
            return "Resume did not complete cleanly; see History for the per-entry results.";
        }

        if (outcome.EndedPause is { } ended)
        {
            return DescribeRun(ended, "Updates resumed; the pause set in the Settings app was ended.");
        }

        return outcome.WasPaused ? "Updates resumed." : "Updates were not paused.";
    }

    private static string DescribeClear(IReadOnlyList<RevertResult> results)
    {
        if (results.Count == 0)
        {
            return "There was no hold to clear.";
        }

        return results.All(r => r.State == RunState.Completed)
            ? "The feature-update hold was cleared."
            : "Clearing the hold did not complete cleanly; see History for the per-entry results.";
    }

    private static string DescribeRepair(UpdateRepairResult result)
    {
        if (!result.Succeeded)
        {
            return string.Create(CultureInfo.CurrentCulture, $"The repair did not complete: {result.Error}");
        }

        return result.SetAside.Count == 0
            ? "Windows Update services were restarted; there was nothing to set aside."
            : string.Create(CultureInfo.CurrentCulture, $"Windows Update was repaired. Set aside: {string.Join(", ", result.SetAside)}.");
    }

    // A run either changed what was asked, was blocked by organisation policy, or failed (and was rolled back).
    private static string DescribeRun(ApplyResult result, string success)
    {
        if (result.Entries.Any(e => e.Result == ApplyState.Managed))
        {
            return ManagedMessage;
        }

        if (result.State == RunState.Completed)
        {
            return success;
        }

        string? note = result.Entries.Select(e => e.Note).FirstOrDefault(n => !string.IsNullOrEmpty(n));
        return string.Create(CultureInfo.CurrentCulture, $"Nothing was changed ({DisplayLabels.For(result.State)}). {note}").TrimEnd();
    }

    partial void OnIsLoadedChanged(bool value) => NotifyControlState();

    partial void OnIsManagedChanged(bool value) => NotifyControlState();

    partial void OnIsBusyChanged(bool value) => NotifyControlState();

    partial void OnIsPausedChanged(bool value) => NotifyControlState();

    partial void OnIsHeldChanged(bool value) => NotifyControlState();

    partial void OnSelectedHoldVersionChanged(string? value) => NotifyControlState();

    partial void OnKnowledgeBaseIdChanged(string value) => NotifyControlState();

    private void NotifyControlState()
    {
        OnPropertyChanged(nameof(CanControl));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(CanHide));
        OnPropertyChanged(nameof(CanHold));
        OnPropertyChanged(nameof(CanClearHold));
        OnPropertyChanged(nameof(CanRepair));
        PauseCommand.NotifyCanExecuteChanged();
        ExtendedPauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
        HideCommand.NotifyCanExecuteChanged();
        UnhideCommand.NotifyCanExecuteChanged();
        HoldCommand.NotifyCanExecuteChanged();
        ClearHoldCommand.NotifyCanExecuteChanged();
        RepairCommand.NotifyCanExecuteChanged();
    }
}
