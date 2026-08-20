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
/// the machine's audit score against each built-in security baseline, and quick
/// actions. Scoring reads the machine, so it never runs on its own — the user
/// starts it with the score button, off the UI thread.
/// </summary>
internal sealed partial class DashboardViewModel : ObservableObject, IPageViewModel
{
    private readonly IAuditCoordinator _audit;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _auditStatus = "This machine has not been scored yet. Select “Score this machine” to audit it against the built-in security baselines.";

    /// <summary>Builds the dashboard over the doctor panel and the audit coordinator.</summary>
    public DashboardViewModel(DoctorViewModel doctor, IAuditCoordinator audit)
    {
        ArgumentNullException.ThrowIfNull(doctor);
        ArgumentNullException.ThrowIfNull(audit);
        Doctor = doctor;
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

    /// <summary>Whether a score run can start (not already running).</summary>
    public bool CanRefresh => !IsBusy;

    /// <summary>Runs the baseline audits and refreshes the scores. User-initiated only.</summary>
    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private Task RefreshScoresAsync() => LoadScoresAsync();

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "UI boundary: a fault while auditing must be shown, not crash the window; the audit is read-only.")]
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

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRefresh));
        RefreshScoresCommand.NotifyCanExecuteChanged();
    }
}
