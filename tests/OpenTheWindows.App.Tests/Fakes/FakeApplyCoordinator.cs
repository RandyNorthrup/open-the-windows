using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.App.Tests.Fakes;

/// <summary>Scriptable <see cref="IApplyCoordinator"/>: returns the plan and result the test set and records the calls.</summary>
internal sealed class FakeApplyCoordinator : IApplyCoordinator
{
    /// <summary>The plan <see cref="Preview"/> returns.</summary>
    public PlanResult PlanToReturn { get; set; } = new("test", [], []);

    /// <summary>The result <see cref="ApplyAsync"/> returns.</summary>
    public ApplyResult ResultToReturn { get; set; } = Empty();

    /// <summary>How many times <see cref="Preview"/> was called.</summary>
    public int PreviewCount { get; private set; }

    /// <summary>How many times <see cref="ApplyAsync"/> was called.</summary>
    public int ApplyCount { get; private set; }

    /// <summary>How many times <see cref="RestartExplorerAsync"/> was called.</summary>
    public int RestartExplorerCount { get; private set; }

    /// <summary>The options passed to the last <see cref="ApplyAsync"/> call.</summary>
    public ApplyOptions? LastApplyOptions { get; private set; }

    /// <summary>When set, <see cref="Preview"/> throws to exercise the flow's fault handling.</summary>
    public bool ThrowOnPreview { get; set; }

    /// <summary>When set, <see cref="ApplyAsync"/> throws to exercise the flow's fault handling.</summary>
    public bool ThrowOnApply { get; set; }

    /// <inheritdoc />
    public PlanResult Preview(IReadOnlyList<TweakDefinition> entries, ApplyOptions options)
    {
        PreviewCount++;
        return ThrowOnPreview ? throw new InvalidOperationException("preview failed") : PlanToReturn;
    }

    /// <inheritdoc />
    public Task<ApplyResult> ApplyAsync(IReadOnlyList<TweakDefinition> entries, ApplyOptions options)
    {
        ApplyCount++;
        LastApplyOptions = options;
        return ThrowOnApply ? throw new InvalidOperationException("apply failed") : Task.FromResult(ResultToReturn);
    }

    /// <inheritdoc />
    public Task RestartExplorerAsync()
    {
        RestartExplorerCount++;
        return Task.CompletedTask;
    }

    private static ApplyResult Empty() => new(
        Guid.Empty,
        RunState.Completed,
        RestartRequirement.None,
        new RestorePointResult(RestorePointStatus.NotRequested, null, "test"),
        new PreflightReport([]),
        new PlanResult("test", [], []),
        []);
}
