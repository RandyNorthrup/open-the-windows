using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Resources;
using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Audit;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// The landing page: system health (the pre-flight <see cref="DoctorViewModel"/>),
/// the machine's audit score against each built-in security baseline (scored on
/// first activation, off the UI thread), and quick actions.
/// </summary>
internal sealed partial class DashboardViewModel : ObservableObject, IPageViewModel, IActivatable
{
    private readonly INavigationService _navigation;
    private readonly IAuditCoordinator _audit;
    private bool _loaded;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _auditStatus = "Baseline scores load when the dashboard opens.";

    /// <summary>Builds the dashboard over the doctor panel, navigation and the audit coordinator.</summary>
    public DashboardViewModel(DoctorViewModel doctor, INavigationService navigation, IAuditCoordinator audit)
    {
        ArgumentNullException.ThrowIfNull(doctor);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(audit);
        Doctor = doctor;
        _navigation = navigation;
        _audit = audit;
    }

    /// <inheritdoc />
    public string Key => PageKeys.Dashboard;

    /// <inheritdoc />
    public string Title => Strings.Nav_Dashboard;

    /// <summary>The system health / pre-flight panel shown on the dashboard.</summary>
    public DoctorViewModel Doctor { get; }

    /// <summary>The machine's audit score against each built-in baseline.</summary>
    public ObservableCollection<BaselineScoreViewModel> BaselineScores { get; } = [];

    /// <summary>Whether a re-score can start (not already running).</summary>
    public bool CanRefresh => !IsBusy;

    /// <inheritdoc />
    public void OnActivated()
    {
        if (!_loaded)
        {
            _ = LoadScoresAsync();
        }
    }

    /// <summary>Re-runs the baseline audits and refreshes the scores.</summary>
    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private Task RefreshScoresAsync() => LoadScoresAsync();

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "UI boundary: a fault while auditing must be shown, not crash the window; the audit is read-only. Recorded in PLAN.md §7.3.")]
    private async Task LoadScoresAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        AuditStatus = "Scoring the machine against the built-in baselines…";
        try
        {
            BaselineScores.Clear();
            foreach (Baseline baseline in _audit.Baselines)
            {
                AuditReport report = await _audit.AuditAsync(baseline).ConfigureAwait(true);
                BaselineScores.Add(new BaselineScoreViewModel(baseline, report));
            }

            _loaded = true;
            AuditStatus = BaselineScores.Count == 0
                ? "No baselines are available."
                : string.Create(CultureInfo.CurrentCulture, $"Scored against {BaselineScores.Count} baseline(s).");
        }
        catch (Exception ex)
        {
            AuditStatus = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Navigates to the page identified by <paramref name="pageKey"/>.</summary>
    [RelayCommand]
    private void Open(string pageKey) => _navigation.NavigateTo(pageKey);

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRefresh));
        RefreshScoresCommand.NotifyCanExecuteChanged();
    }
}
