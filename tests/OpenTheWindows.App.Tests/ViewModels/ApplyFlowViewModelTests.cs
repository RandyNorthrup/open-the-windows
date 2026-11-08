using OpenTheWindows.App.Tests.Fakes;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.App.Tests.ViewModels;

/// <summary>The review-and-apply flow: preview, Breaking gating, apply, failure and restart handling.</summary>
public sealed class ApplyFlowViewModelTests
{
    private static readonly ApplyOptions Options = new(false, true, false, true, true, "test", false);
    private static readonly IReadOnlyList<TweakDefinition> NoEntries = [];

    private readonly FakeApplyCoordinator _coordinator = new();
    private readonly FakeDialogService _dialog = new();

    private ApplyFlowViewModel Build() => new(_coordinator, _dialog, new AppSettings());

    private static PlannedEntry Planned(string id, PlanDisposition disposition, RiskTier risk = RiskTier.Safe, RestartRequirement requires = RestartRequirement.None) => new(new TweakId(id), disposition, risk, requires, [], null);

    // Plans one entry of the given risk, lets the confirmation dialogs answer yes, and applies.
    private async Task<ApplyFlowViewModel> ApplyConfirmedAsync(string id, RiskTier risk)
    {
        _coordinator.PlanToReturn = new PlanResult("t", [Planned(id, PlanDisposition.Apply, risk)], []);
        _coordinator.ResultToReturn = Result(RunState.Completed, RestartRequirement.None);
        _dialog.ConfirmResult = true;
        _dialog.ConfirmTypedResult = true;
        ApplyFlowViewModel flow = Build();
        await flow.StartAsync("Privacy", NoEntries, Options).ConfigureAwait(false);
        await flow.ApplyCommand.ExecuteAsync(null).ConfigureAwait(false);
        return flow;
    }

    private static ApplyResult Result(RunState state, RestartRequirement requires, params EntryOutcome[] outcomes) => new(
        Guid.Empty, state, requires, new RestorePointResult(RestorePointStatus.NotRequested, null, "t"),
        new PreflightReport([]), new PlanResult("t", [], []), outcomes);

    [Fact]
    public async Task Start_shows_the_plan_and_enables_apply_when_something_will_change()
    {
        _coordinator.PlanToReturn = new PlanResult("t", [Planned("a.b.one", PlanDisposition.Apply)], []);
        ApplyFlowViewModel flow = Build();

        await flow.StartAsync("Privacy", NoEntries, Options);

        Assert.Equal(ApplyFlowStage.Preview, flow.Stage);
        Assert.Single(flow.PlanEntries);
        Assert.True(flow.CanApply);
    }

    [Fact]
    public async Task A_plan_with_errors_cannot_be_applied()
    {
        _coordinator.PlanToReturn = new PlanResult("t", [Planned("a.b.one", PlanDisposition.Conflict)], ["conflict"]);
        ApplyFlowViewModel flow = Build();

        await flow.StartAsync("Privacy", NoEntries, Options);

        Assert.False(flow.CanApply);
    }

    [Fact]
    public async Task Apply_runs_the_engine_and_shows_the_outcomes()
    {
        _coordinator.PlanToReturn = new PlanResult("t", [Planned("a.b.one", PlanDisposition.Apply)], []);
        _coordinator.ResultToReturn = Result(RunState.Completed, RestartRequirement.None,
            new EntryOutcome(new TweakId("a.b.one"), ApplyState.Applied, RiskTier.Safe, RestartRequirement.None, null));
        ApplyFlowViewModel flow = Build();
        await flow.StartAsync("Privacy", NoEntries, Options);

        await flow.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(1, _coordinator.ApplyCount);
        Assert.Equal(ApplyFlowStage.Completed, flow.Stage);
        Assert.Single(flow.Outcomes);
    }

    [Fact]
    public async Task A_breaking_entry_requires_a_typed_confirmation()
    {
        _coordinator.PlanToReturn = new PlanResult("t", [Planned("a.b.break", PlanDisposition.Apply, RiskTier.Breaking)], []);
        _dialog.ConfirmTypedResult = false;
        ApplyFlowViewModel flow = Build();
        await flow.StartAsync("Privacy", NoEntries, Options);

        await flow.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(1, _dialog.ConfirmTypedCount);
        Assert.Equal("a.b.break", _dialog.LastTypedRequired);
        Assert.Equal(0, _coordinator.ApplyCount);
        Assert.Equal(ApplyFlowStage.Preview, flow.Stage);
    }

    [Fact]
    public async Task A_confirmed_breaking_entry_is_applied()
    {
        ApplyFlowViewModel flow = await ApplyConfirmedAsync("a.b.break", RiskTier.Breaking);

        Assert.Equal(1, _dialog.ConfirmTypedCount);
        Assert.Equal(0, _dialog.ConfirmCount);
        Assert.Equal(1, _coordinator.ApplyCount);
        Assert.Equal(ApplyFlowStage.Completed, flow.Stage);
    }

    [Fact]
    public async Task A_rolled_back_run_lands_in_the_failed_stage()
    {
        _coordinator.PlanToReturn = new PlanResult("t", [Planned("a.b.one", PlanDisposition.Apply)], []);
        _coordinator.ResultToReturn = Result(RunState.RolledBack, RestartRequirement.None);
        ApplyFlowViewModel flow = Build();
        await flow.StartAsync("Privacy", NoEntries, Options);

        await flow.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(ApplyFlowStage.Failed, flow.Stage);
    }

    [Fact]
    public async Task An_explorer_restart_is_offered_when_needed_and_not_done_automatically()
    {
        _coordinator.PlanToReturn = new PlanResult("t", [Planned("a.b.one", PlanDisposition.Apply, RiskTier.Safe, RestartRequirement.ExplorerRestart)], []);
        _coordinator.ResultToReturn = Result(RunState.Completed, RestartRequirement.ExplorerRestart);
        ApplyFlowViewModel flow = Build();
        await flow.StartAsync("Shell", NoEntries, Options);
        flow.RestartExplorer = false;

        await flow.ApplyCommand.ExecuteAsync(null);

        Assert.True(flow.ExplorerRestartAvailable);
        await flow.RestartExplorerNowCommand.ExecuteAsync(null);
        Assert.Equal(1, _coordinator.RestartExplorerCount);
        Assert.False(flow.ExplorerRestartAvailable);
    }

    [Fact]
    public async Task A_fault_while_previewing_is_shown_as_a_failed_stage()
    {
        _coordinator.ThrowOnPreview = true;
        ApplyFlowViewModel flow = Build();

        await flow.StartAsync("Privacy", NoEntries, Options);

        Assert.Equal(ApplyFlowStage.Failed, flow.Stage);
        Assert.False(flow.CanApply);
        Assert.Contains("preview failed", flow.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_fault_while_applying_is_shown_as_a_failed_stage()
    {
        _coordinator.PlanToReturn = new PlanResult("t", [Planned("a.b.one", PlanDisposition.Apply)], []);
        _coordinator.ThrowOnApply = true;
        ApplyFlowViewModel flow = Build();
        await flow.StartAsync("Privacy", NoEntries, Options);

        await flow.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(ApplyFlowStage.Failed, flow.Stage);
        Assert.Contains("apply failed", flow.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void The_restart_explorer_default_comes_from_settings()
    {
        ApplyFlowViewModel flow = new(_coordinator, _dialog, new AppSettings { RestartExplorerAutomatically = true });

        Assert.True(flow.RestartExplorer);
    }

    [Fact]
    public void A_new_flow_starts_in_the_planning_stage_and_cannot_apply()
    {
        ApplyFlowViewModel flow = Build();

        Assert.Equal(ApplyFlowStage.Planning, flow.Stage);
        Assert.False(flow.CanApply);
    }

    [Fact]
    public async Task Advanced_entries_need_an_acknowledgement_before_applying()
    {
        _coordinator.PlanToReturn = new PlanResult("t", [Planned("a.b.adv", PlanDisposition.Apply, RiskTier.Advanced), Planned("a.b.adv2", PlanDisposition.Apply, RiskTier.Advanced)], []);
        _dialog.ConfirmResult = false;
        ApplyFlowViewModel flow = Build();
        await flow.StartAsync("Privacy", NoEntries, Options);

        await flow.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(1, _dialog.ConfirmCount);
        Assert.Equal(0, _dialog.ConfirmTypedCount);
        Assert.Equal(0, _coordinator.ApplyCount);
        Assert.Equal(ApplyFlowStage.Preview, flow.Stage);
    }

    [Fact]
    public async Task Acknowledged_advanced_entries_are_applied()
    {
        ApplyFlowViewModel flow = await ApplyConfirmedAsync("a.b.adv", RiskTier.Advanced);

        Assert.Equal(1, _dialog.ConfirmCount);
        Assert.Equal(0, _dialog.ConfirmTypedCount);
        Assert.Equal(1, _coordinator.ApplyCount);
        Assert.Equal(ApplyFlowStage.Completed, flow.Stage);
    }

    [Fact]
    public async Task A_safe_only_run_asks_nothing()
    {
        _coordinator.PlanToReturn = new PlanResult("t", [Planned("a.b.one", PlanDisposition.Apply)], []);
        ApplyFlowViewModel flow = Build();
        await flow.StartAsync("Privacy", NoEntries, Options);

        await flow.ApplyCommand.ExecuteAsync(null);

        Assert.Equal(0, _dialog.ConfirmCount);
        Assert.Equal(0, _dialog.ConfirmTypedCount);
    }

    [Fact]
    public async Task A_profile_asking_for_an_explorer_restart_pre_ticks_it()
    {
        ApplyFlowViewModel flow = Build();

        await flow.StartAsync("Profile", NoEntries, Options with { RestartExplorer = true });

        Assert.True(flow.RestartExplorer);
    }

    [Fact]
    public void Rejects_null_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new ApplyFlowViewModel(null!, _dialog, new AppSettings()));
        Assert.Throws<ArgumentNullException>(() => new ApplyFlowViewModel(_coordinator, null!, new AppSettings()));
        Assert.Throws<ArgumentNullException>(() => new ApplyFlowViewModel(_coordinator, _dialog, null!));
    }
}
