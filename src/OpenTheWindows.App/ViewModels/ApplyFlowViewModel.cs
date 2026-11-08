using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenTheWindows.App.Resources;
using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// Drives the review-and-apply dialog: it previews the run (the what-if plan) off the UI
/// thread, asks the user to acknowledge Advanced entries and to type the id of every
/// Breaking one, applies the run on a background thread, and shows the per-entry outcome
/// with the restart requirement. An engine fault is surfaced as a failed run rather than
/// crashing the UI.
/// </summary>
internal sealed partial class ApplyFlowViewModel : ObservableObject
{
    private readonly IApplyCoordinator _coordinator;
    private readonly IDialogService _dialog;
    private IReadOnlyList<TweakDefinition> _entries = [];
    private ApplyOptions _options = new(false, true, false, true, true, "GUI", false);
    private IReadOnlyList<string> _breakingIds = [];
    private IReadOnlyList<string> _advancedIds = [];
    private bool _willApply;
    private bool _hasErrors;

    [ObservableProperty]
    private ApplyFlowStage _stage = ApplyFlowStage.Planning;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _restartExplorer;

    [ObservableProperty]
    private bool _explorerRestartWouldBeNeeded;

    [ObservableProperty]
    private bool _explorerRestartAvailable;

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private string _restartRequirementLabel = string.Empty;

    /// <summary>Creates the flow over the apply coordinator, the dialog service and settings.</summary>
    public ApplyFlowViewModel(IApplyCoordinator coordinator, IDialogService dialog, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(settings);
        _coordinator = coordinator;
        _dialog = dialog;
        _restartExplorer = settings.RestartExplorerAutomatically;
    }

    /// <summary>The what-if plan rows.</summary>
    public ObservableCollection<ApplyPlanEntryViewModel> PlanEntries { get; } = [];

    /// <summary>The per-entry results after an apply.</summary>
    public ObservableCollection<ApplyOutcomeEntryViewModel> Outcomes { get; } = [];

    /// <summary>Whether the run can be applied (there is something to change and no plan errors).</summary>
    public bool CanApply => Stage == ApplyFlowStage.Preview && _willApply && !_hasErrors;

    /// <summary>
    /// Builds the plan for a selection on a background thread and shows the preview. Never
    /// throws: a fault while reading the machine lands the flow in the Failed stage.
    /// </summary>
    public async Task StartAsync(string title, IReadOnlyList<TweakDefinition> entries, ApplyOptions options)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(options);
        Title = title;
        _entries = entries;
        _options = options;
        Summary = "Reading this PC's current settings…";
        Stage = ApplyFlowStage.Planning;

        // A profile that asks for an Explorer restart pre-ticks it; the user can still untick it.
        RestartExplorer |= options.RestartExplorer;

        try
        {
            ShowPlan(await _coordinator.PreviewAsync(entries, options).ConfigureAwait(true));
        }
        catch (Exception ex) when (UiFaults.IsReportable(ex))
        {
            _willApply = false;
            _hasErrors = true;
            Summary = ex.Message;
            Stage = ApplyFlowStage.Failed;
        }
    }

    private void ShowPlan(PlanResult plan)
    {
        PlanEntries.Clear();
        foreach (PlannedEntry entry in plan.Entries)
        {
            PlanEntries.Add(new ApplyPlanEntryViewModel(entry));
        }

        _willApply = plan.WillApply.Any();
        _hasErrors = plan.HasErrors;
        List<PlannedEntry> changing = [.. plan.Entries.Where(e => e.Disposition is PlanDisposition.Apply or PlanDisposition.ManagedBreakGlass)];
        _breakingIds = [.. changing.Where(e => e.Risk >= RiskTier.Breaking).Select(e => e.Id.Value)];
        _advancedIds = [.. changing.Where(e => e.Risk == RiskTier.Advanced).Select(e => e.Id.Value)];
        ExplorerRestartWouldBeNeeded = plan.Requires == RestartRequirement.ExplorerRestart;
        Summary = plan.HasErrors
            ? string.Join(" ", plan.Errors)
            : string.Create(CultureInfo.CurrentCulture, $"{PlanEntries.Count(p => p.WillApply)} of {PlanEntries.Count} entries will change.");
        Stage = ApplyFlowStage.Preview;
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        if (!Acknowledged())
        {
            return;
        }

        Summary = "Applying changes…";
        Stage = ApplyFlowStage.Applying;
        try
        {
            ApplyOptions options = _options with { RestartExplorer = RestartExplorer };
            ApplyResult result = await _coordinator.ApplyAsync(_entries, options).ConfigureAwait(true);
            ShowResult(result);
        }
        catch (Exception ex) when (UiFaults.IsReportable(ex))
        {
            Summary = ex.Message;
            Stage = ApplyFlowStage.Failed;
        }
    }

    // Advanced entries are acknowledged once, together; every Breaking entry needs its id typed.
    private bool Acknowledged()
    {
        if (_advancedIds.Count > 0)
        {
            string message = string.Create(CultureInfo.CurrentCulture,
                $"This run includes {_advancedIds.Count} Advanced change(s) that can affect how Windows or your apps behave:{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, _advancedIds)}{Environment.NewLine}{Environment.NewLine}Each is recorded and can be reverted from History. Continue?");
            if (!_dialog.Confirm(Title, message))
            {
                return false;
            }
        }

        foreach (string id in _breakingIds)
        {
            string message = string.Create(CultureInfo.CurrentCulture,
                $"“{id}” is a Breaking change and can disrupt the system. Type its id to confirm.");
            if (!_dialog.ConfirmTyped(Title, message, id))
            {
                return false;
            }
        }

        return true;
    }

    [RelayCommand]
    private async Task RestartExplorerNowAsync()
    {
        await _coordinator.RestartExplorerAsync().ConfigureAwait(true);
        ExplorerRestartAvailable = false;
    }

    private void ShowResult(ApplyResult result)
    {
        Outcomes.Clear();
        foreach (EntryOutcome outcome in result.Entries)
        {
            Outcomes.Add(new ApplyOutcomeEntryViewModel(outcome));
        }

        int applied = result.Entries.Count(e => e.Result == ApplyState.Applied);
        RestartRequirementLabel = DisplayLabels.For(result.Requires);
        ExplorerRestartAvailable = result.State == RunState.Completed
            && result.Requires == RestartRequirement.ExplorerRestart
            && !RestartExplorer;
        Summary = result.State switch
        {
            RunState.Completed => string.Create(CultureInfo.CurrentCulture, $"Applied {applied} entr(y/ies)."),
            RunState.RolledBack => "The run failed and was rolled back; the machine is unchanged.",
            _ => "The run failed.",
        };
        Stage = result.State == RunState.Completed ? ApplyFlowStage.Completed : ApplyFlowStage.Failed;
    }

    partial void OnStageChanged(ApplyFlowStage value)
    {
        OnPropertyChanged(nameof(CanApply));
        ApplyCommand.NotifyCanExecuteChanged();
    }
}
