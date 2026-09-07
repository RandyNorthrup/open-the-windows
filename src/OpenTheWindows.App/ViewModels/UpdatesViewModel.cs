using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Resources;
using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Updates;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// The Updates page: the current release and its end-of-servicing, the pause
/// state, quick pause presets, an extended pause behind a warning, and resume.
/// When updates are managed by organisation policy the controls are read-only.
/// Reading the status probes the machine, so it never runs on its own — the user
/// loads it with the refresh button.
/// </summary>
internal sealed partial class UpdatesViewModel : ObservableObject, IPageViewModel
{
    private const int SupportedMaxPauseDays = 35;
    private const string NotLoaded = "Not loaded — select “Refresh status”.";

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
    private bool _isPaused;

    [ObservableProperty]
    private bool _isManaged;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _extendedDays = 90;

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

    /// <summary>Whether the pause/resume controls are usable (not managed, not mid-operation).</summary>
    public bool CanControl => !IsManaged && !IsBusy;

    /// <summary>Whether Resume is available (currently paused and controllable).</summary>
    public bool CanResume => IsPaused && CanControl;

    /// <summary>Whether hide/unhide can run (not managed, not busy, KB looks valid).</summary>
    public bool CanHide => CanControl && Core.Updates.KnowledgeBaseId.Normalize(KnowledgeBaseId) is not null;

    /// <summary>Reads and shows the update status. User-initiated only.</summary>
    [RelayCommand]
    private void Refresh()
    {
        UpdateStatus status = _coordinator.Status();
        CurrentVersion = string.Create(CultureInfo.CurrentCulture, $"{status.CurrentVersion} ({status.EditionId})");
        EndOfService = status.EndOfServiceDate is { } date
            ? string.Create(CultureInfo.CurrentCulture, $"{date:yyyy-MM-dd} ({status.DaysUntilEndOfService} days)")
            : "Unknown";
        EndOfServiceWarning = status.CurrentVersionWithinWarning;
        IsPaused = status.IsPaused;
        PauseState = status is { IsPaused: true, PausedUntil: { } until }
            ? string.Create(CultureInfo.CurrentCulture, $"Paused until {until.LocalDateTime:yyyy-MM-dd}")
            : "Not paused";
        TargetRelease = status.TargetReleaseVersion is { } pin
            ? string.Create(CultureInfo.CurrentCulture, $"Pinned to {pin}")
            : "Not pinned";
        IsManaged = _coordinator.IsManaged();
    }

    /// <summary>Pauses updates for a supported preset number of days.</summary>
    [RelayCommand(CanExecute = nameof(CanControl))]
    private async Task PauseAsync(int days)
    {
        await RunAsync(() => _coordinator.PauseAsync(days, extended: false)).ConfigureAwait(true);
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

        await RunAsync(() => _coordinator.PauseAsync(ExtendedDays, extended: true)).ConfigureAwait(true);
    }

    /// <summary>Resumes updates now.</summary>
    [RelayCommand(CanExecute = nameof(CanResume))]
    private async Task ResumeAsync()
    {
        await RunAsync(_coordinator.ResumeAsync).ConfigureAwait(true);
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
        try
        {
            UpdateHideOutcome outcome = hide
                ? await _coordinator.HideAsync(KnowledgeBaseId).ConfigureAwait(true)
                : await _coordinator.UnhideAsync(KnowledgeBaseId).ConfigureAwait(true);
            HideStatus = outcome.Message;
            if (outcome.Succeeded)
            {
                await RefreshHiddenAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunAsync(Func<Task> operation)
    {
        IsBusy = true;
        try
        {
            await operation().ConfigureAwait(true);
            Refresh();
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnIsManagedChanged(bool value) => NotifyControlState();

    partial void OnIsBusyChanged(bool value) => NotifyControlState();

    partial void OnIsPausedChanged(bool value) => NotifyControlState();

    private void NotifyControlState()
    {
        OnPropertyChanged(nameof(CanControl));
        OnPropertyChanged(nameof(CanResume));
        PauseCommand.NotifyCanExecuteChanged();
        ExtendedPauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
        HideCommand.NotifyCanExecuteChanged();
        UnhideCommand.NotifyCanExecuteChanged();
    }

    partial void OnKnowledgeBaseIdChanged(string value)
    {
        OnPropertyChanged(nameof(CanHide));
        HideCommand.NotifyCanExecuteChanged();
        UnhideCommand.NotifyCanExecuteChanged();
    }
}
