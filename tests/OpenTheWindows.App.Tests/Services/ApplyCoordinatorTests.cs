using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.App.Tests.Services;

/// <summary>The coordinator delegates preview/apply to the engine and restarts Explorer for the interactive user.</summary>
public sealed class ApplyCoordinatorTests
{
    private static readonly ApplyOptions Options = new(false, false, false, true, true, "test", false);
    private static readonly IReadOnlyList<TweakDefinition> NoEntries = [];

    private readonly FakeMachine _machine = new() { User = new InteractiveUser("S-1-5-21-1-2-3-1001", "tester", true) };
    private readonly FakeExplorerRestarter _explorer = new();

    private ApplyCoordinator Build()
    {
        ApplyHarness harness = FakeApplyEngine.Create(_machine);
        return new ApplyCoordinator(() => harness.Engine, _explorer, _machine);
    }

    [Fact]
    public void Preview_returns_a_clean_plan_for_an_empty_selection()
    {
        PlanResult plan = Build().Preview(NoEntries, Options);

        Assert.False(plan.HasErrors);
        Assert.Empty(plan.Entries);
    }

    [Fact]
    public async Task Apply_runs_the_engine_and_completes()
    {
        ApplyResult result = await Build().ApplyAsync(NoEntries, Options);

        Assert.Equal(RunState.Completed, result.State);
    }

    [Fact]
    public async Task Restart_explorer_targets_the_interactive_user()
    {
        await Build().RestartExplorerAsync();

        Assert.Contains("S-1-5-21-1-2-3-1001", _explorer.Restarts, StringComparer.Ordinal);
    }

    [Fact]
    public void Rejects_null_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new ApplyCoordinator(null!, _explorer, _machine));
        Assert.Throws<ArgumentNullException>(() => new ApplyCoordinator(() => null!, null!, _machine));
        Assert.Throws<ArgumentNullException>(() => new ApplyCoordinator(() => null!, _explorer, null!));
    }
}
