using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Resources;
using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Persistence;
using OpenTheWindows.Core.Startup;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// The Startup page: the apps Windows starts at sign-in with the same on/off switch Task
/// Manager uses (each change journaled and revertable from History), and the persistence
/// watch — record every autostart as a baseline, then see what was added, removed or
/// changed since. The app list loads when the page is shown; the watch runs only on request.
/// </summary>
internal sealed partial class StartupViewModel : ObservableObject, IPageViewModel, IActivatable
{
    private readonly IActivityCoordinator _coordinator;
    private readonly IDialogService _dialog;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTurnOn), nameof(CanTurnOff))]
    [NotifyCanExecuteChangedFor(nameof(TurnOnCommand), nameof(TurnOffCommand))]
    private StartupAppItemViewModel? _selectedApp;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTurnOn), nameof(CanTurnOff), nameof(CanWatch))]
    [NotifyCanExecuteChangedFor(nameof(TurnOnCommand), nameof(TurnOffCommand), nameof(TakeSnapshotCommand), nameof(CompareCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private string _watchStatus = "Record a baseline, then compare later to see what started running automatically since.";

    /// <summary>Creates the page over the activity coordinator and the dialog service.</summary>
    public StartupViewModel(IActivityCoordinator coordinator, IDialogService dialog)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(dialog);
        _coordinator = coordinator;
        _dialog = dialog;
    }

    /// <inheritdoc />
    public string Key => PageKeys.Startup;

    /// <inheritdoc />
    public string Title => Strings.Nav_Startup;

    /// <summary>The startup apps.</summary>
    public ObservableCollection<StartupAppItemViewModel> Apps { get; } = [];

    /// <summary>What changed since the baseline (after a comparison).</summary>
    public ObservableCollection<PersistenceChangeViewModel> Changes { get; } = [];

    /// <summary>Whether the selected app can be turned on.</summary>
    public bool CanTurnOn => !IsBusy && SelectedApp is { Enabled: false };

    /// <summary>Whether the selected app can be turned off.</summary>
    public bool CanTurnOff => !IsBusy && SelectedApp is { Enabled: true };

    /// <summary>Whether a baseline can be taken or compared now.</summary>
    public bool CanWatch => !IsBusy;

    /// <inheritdoc />
    public void OnActivated() => RefreshCommand.Execute(null);

    /// <summary>Reloads the startup apps.</summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IReadOnlyList<StartupApp> apps = await _coordinator.ListStartupAsync().ConfigureAwait(true);
            Apps.Clear();
            foreach (StartupApp app in apps)
            {
                Apps.Add(new StartupAppItemViewModel(app));
            }

            Status = apps.Count == 0
                ? "No startup apps found."
                : string.Create(CultureInfo.CurrentCulture, $"{apps.Count} startup app(s), {apps.Count(a => !a.Enabled)} turned off.");
        }
        catch (Exception ex) when (UiFaults.IsReportable(ex))
        {
            Status = string.Create(CultureInfo.CurrentCulture, $"Could not read the startup apps: {ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanTurnOn))]
    private async Task TurnOnAsync() => await ToggleAsync(enable: true).ConfigureAwait(true);

    [RelayCommand(CanExecute = nameof(CanTurnOff))]
    private async Task TurnOffAsync()
    {
        if (SelectedApp is { } item && _dialog.Confirm(Title, string.Create(CultureInfo.CurrentCulture,
                $"Stop “{item.Name}” starting when you sign in? It stays installed, and you can turn it back on here or revert the change from History.")))
        {
            await ToggleAsync(enable: false).ConfigureAwait(true);
        }
    }

    private async Task ToggleAsync(bool enable)
    {
        if (SelectedApp is not { } item)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _coordinator.SetStartupEnabledAsync(item.App, enable).ConfigureAwait(true);
            string verb = enable ? "will start" : "will no longer start";
            Status = result.State == Core.Journal.RunState.Completed
                ? string.Create(CultureInfo.CurrentCulture, $"“{item.Name}” {verb} at your next sign-in.")
                : string.Create(CultureInfo.CurrentCulture, $"Nothing was changed ({DisplayLabels.For(result.State)}). {result.Entries.Select(e => e.Note).FirstOrDefault(n => !string.IsNullOrEmpty(n))}").TrimEnd();
        }
        catch (Exception ex) when (UiFaults.IsReportable(ex))
        {
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }

        string keep = Status;
        await RefreshAsync().ConfigureAwait(true);
        Status = keep;
    }

    [RelayCommand(CanExecute = nameof(CanWatch))]
    private async Task TakeSnapshotAsync()
    {
        if (!_dialog.Confirm(Title, "Record every service, scheduled task, Run entry and Startup-folder item now as the baseline? This replaces any earlier baseline."))
        {
            return;
        }

        await WatchAsync(async () =>
        {
            PersistenceSnapshot snapshot = await _coordinator.TakeSnapshotAsync().ConfigureAwait(true);
            Changes.Clear();
            return string.Create(CultureInfo.CurrentCulture, $"Recorded {snapshot.Items.Count} autostarts as the baseline ({snapshot.TakenAt.LocalDateTime:g}).");
        }).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanWatch))]
    private async Task CompareAsync()
    {
        await WatchAsync(async () =>
        {
            PersistenceComparison? comparison = await _coordinator.CompareAsync().ConfigureAwait(true);
            Changes.Clear();
            if (comparison is null)
            {
                return "There is no baseline yet. Record one first.";
            }

            foreach (PersistenceChange change in comparison.Changes)
            {
                Changes.Add(new PersistenceChangeViewModel(change));
            }

            return comparison.Changes.Count == 0
                ? string.Create(CultureInfo.CurrentCulture, $"Nothing changed since the baseline of {comparison.Baseline.TakenAt.LocalDateTime:g}.")
                : string.Create(CultureInfo.CurrentCulture, $"{comparison.Changes.Count} change(s) since the baseline of {comparison.Baseline.TakenAt.LocalDateTime:g}.");
        }).ConfigureAwait(true);
    }

    private async Task WatchAsync(Func<Task<string>> operation)
    {
        IsBusy = true;
        WatchStatus = "Reading every autostart…";
        try
        {
            WatchStatus = await operation().ConfigureAwait(true);
        }
        catch (Exception ex) when (UiFaults.IsReportable(ex))
        {
            WatchStatus = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
