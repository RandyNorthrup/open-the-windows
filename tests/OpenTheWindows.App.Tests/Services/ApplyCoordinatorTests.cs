using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.App.Tests.Services;

/// <summary>
/// The coordinator delegates preview, scan, apply, history, run detail and revert to the
/// real engines over an in-memory machine, and restarts Explorer for the interactive user.
/// </summary>
public sealed class ApplyCoordinatorTests
{
    private static readonly ApplyOptions Options = new(false, false, false, true, true, "test", false);
    private static readonly IReadOnlyList<TweakDefinition> NoEntries = [];
    private static readonly TweakCatalog Catalog = CatalogLoader.LoadBuiltIn().Catalog!;

    private readonly FakeMachine _machine = new() { User = new InteractiveUser("S-1-5-21-1-2-3-1001", "tester", true) };
    private readonly FakeExplorerRestarter _explorer = new();

    private ApplyCoordinator Build()
    {
        ApplyHarness harness = FakeApplyEngine.Create(_machine);
        return new ApplyCoordinator(() => harness.Engine, () => harness.Scan, _explorer, _machine);
    }

    // A Verified, machine-scope Safe registry entry the fake machine can apply and revert.
    private static TweakDefinition RegistryEntry() => Catalog.Entries.First(e =>
        e.Status == TweakStatus.Verified && e.Scope == Core.Model.Scope.Machine && e.Risk == Core.Model.RiskTier.Safe
        && e.Requires == Core.Model.RestartRequirement.None && e.Actions.All(a => a is RegistryAction));

    [Fact]
    public async Task Preview_returns_a_clean_plan_for_an_empty_selection()
    {
        PlanResult plan = await Build().PreviewAsync(NoEntries, Options);

        Assert.False(plan.HasErrors);
        Assert.Empty(plan.Entries);
    }

    [Fact]
    public async Task Scan_reports_an_entry_not_yet_applied_as_drift()
    {
        TweakDefinition entry = RegistryEntry();

        ScanReport report = await Build().ScanAsync([entry]);

        Assert.Equal(ComplianceState.Drift, Assert.Single(report.Results).State);
    }

    [Fact]
    public async Task Apply_runs_the_engine_and_completes()
    {
        ApplyResult result = await Build().ApplyAsync(NoEntries, Options);

        Assert.Equal(RunState.Completed, result.State);
    }

    [Fact]
    public async Task An_applied_run_is_listed_loaded_and_reverted_entry_by_entry_then_wholesale()
    {
        ApplyCoordinator coordinator = Build();
        TweakDefinition entry = RegistryEntry();
        ApplyResult applied = await coordinator.ApplyAsync([entry], Options);

        JournalSummary run = Assert.Single(coordinator.History());
        JournalRecord record = await coordinator.LoadRunAsync(run.RunId);
        RevertResult one = await coordinator.RevertAsync(applied.RunId, new RevertOptions(false, false, EntryIds: [entry.Id.Value]));
        IReadOnlyList<(JournalSummary Run, RevertResult Result)> everything = await coordinator.RevertEverythingAsync(new RevertOptions(false, false));

        Assert.Equal(entry.Id.Value, Assert.Single(record.Entries).Id);
        Assert.Equal(RunState.Completed, one.State);
        Assert.Empty(everything);
    }

    [Fact]
    public async Task Restart_explorer_targets_the_interactive_user()
    {
        await Build().RestartExplorerAsync();

        Assert.Contains("S-1-5-21-1-2-3-1001", _explorer.Restarts, StringComparer.Ordinal);
    }

    [Fact]
    public void History_is_empty_before_any_run()
    {
        Assert.Empty(Build().History());
    }

    [Fact]
    public async Task Null_arguments_are_rejected()
    {
        ApplyCoordinator coordinator = Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => coordinator.PreviewAsync(null!, Options));
        await Assert.ThrowsAsync<ArgumentNullException>(() => coordinator.PreviewAsync(NoEntries, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => coordinator.ScanAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => coordinator.ApplyAsync(null!, Options));
        await Assert.ThrowsAsync<ArgumentNullException>(() => coordinator.RevertAsync(Guid.Empty, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => coordinator.RevertEverythingAsync(null!));
    }

    [Fact]
    public void Rejects_null_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new ApplyCoordinator(null!, () => null!, _explorer, _machine));
        Assert.Throws<ArgumentNullException>(() => new ApplyCoordinator(() => null!, null!, _explorer, _machine));
        Assert.Throws<ArgumentNullException>(() => new ApplyCoordinator(() => null!, () => null!, null!, _machine));
        Assert.Throws<ArgumentNullException>(() => new ApplyCoordinator(() => null!, () => null!, _explorer, null!));
    }
}
