using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.App.Tests.Fakes;

/// <summary>Scriptable <see cref="IApplyCoordinator"/>: returns the plan, scan and results the test set and records the calls.</summary>
internal sealed class FakeApplyCoordinator : IApplyCoordinator
{
    /// <summary>The plan <see cref="PreviewAsync"/> returns.</summary>
    public PlanResult PlanToReturn { get; set; } = new("test", [], []);

    /// <summary>The report <see cref="ScanAsync"/> returns.</summary>
    public ScanReport ScanToReturn { get; set; } = new(
        new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero), FakeOperatingSystemInfo.Windows11Pro24H2(), "test", []);

    /// <summary>The result <see cref="ApplyAsync"/> returns.</summary>
    public ApplyResult ResultToReturn { get; set; } = TestResults.EmptyApplyResult();

    /// <summary>How many times <see cref="PreviewAsync"/> was called.</summary>
    public int PreviewCount { get; private set; }

    /// <summary>The entries passed to the last <see cref="ScanAsync"/> call.</summary>
    public IReadOnlyList<TweakDefinition>? LastScanned { get; private set; }

    /// <summary>How many times <see cref="ApplyAsync"/> was called.</summary>
    public int ApplyCount { get; private set; }

    /// <summary>How many times <see cref="RestartExplorerAsync"/> was called.</summary>
    public int RestartExplorerCount { get; private set; }

    /// <summary>The options passed to the last <see cref="ApplyAsync"/> call.</summary>
    public ApplyOptions? LastApplyOptions { get; private set; }

    /// <summary>When set, <see cref="PreviewAsync"/> faults to exercise the flow's fault handling.</summary>
    public bool ThrowOnPreview { get; set; }

    /// <summary>When set, <see cref="ScanAsync"/> faults to exercise the page's fault handling.</summary>
    public bool ThrowOnScan { get; set; }

    /// <summary>When set, <see cref="ApplyAsync"/> throws to exercise the flow's fault handling.</summary>
    public bool ThrowOnApply { get; set; }

    /// <summary>When set, <see cref="LoadRunAsync"/> faults (as a journal that does not verify would).</summary>
    public bool ThrowOnLoadRun { get; set; }

    /// <summary>When set, <see cref="RevertAsync"/> and <see cref="RevertEverythingAsync"/> fault.</summary>
    public bool ThrowOnRevert { get; set; }

    /// <summary>The runs <see cref="History"/> returns.</summary>
    public IReadOnlyList<JournalSummary> HistoryToReturn { get; set; } = [];

    /// <summary>The journals <see cref="LoadRunAsync"/> returns, by run id.</summary>
    public Dictionary<Guid, JournalRecord> Runs { get; } = [];

    /// <summary>The result <see cref="RevertAsync"/> returns.</summary>
    public RevertResult RevertResultToReturn { get; set; } = new(Guid.Empty, Guid.Empty, RunState.Completed, []);

    /// <summary>The results <see cref="RevertEverythingAsync"/> returns.</summary>
    public IReadOnlyList<(JournalSummary Run, RevertResult Result)> EverythingToReturn { get; set; } = [];

    /// <summary>How many times <see cref="RevertAsync"/> was called.</summary>
    public int RevertCount { get; private set; }

    /// <summary>How many times <see cref="RevertEverythingAsync"/> was called.</summary>
    public int RevertEverythingCount { get; private set; }

    /// <summary>The run id of the last <see cref="RevertAsync"/> call.</summary>
    public Guid LastRevertRunId { get; private set; }

    /// <summary>The options of the last <see cref="RevertAsync"/> call.</summary>
    public RevertOptions? LastRevertOptions { get; private set; }

    /// <inheritdoc />
    public Task<PlanResult> PreviewAsync(IReadOnlyList<TweakDefinition> entries, ApplyOptions options)
    {
        PreviewCount++;
        return ThrowOnPreview ? Task.FromException<PlanResult>(new InvalidOperationException("preview failed")) : Task.FromResult(PlanToReturn);
    }

    /// <inheritdoc />
    public Task<ScanReport> ScanAsync(IReadOnlyList<TweakDefinition> entries)
    {
        LastScanned = entries;
        return ThrowOnScan ? Task.FromException<ScanReport>(new InvalidOperationException("scan failed")) : Task.FromResult(ScanToReturn);
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

    /// <inheritdoc />
    public IReadOnlyList<JournalSummary> History() => HistoryToReturn;

    /// <inheritdoc />
    public Task<JournalRecord> LoadRunAsync(Guid runId)
        => ThrowOnLoadRun
            ? Task.FromException<JournalRecord>(new JournalIntegrityException(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Run {runId} journal does not verify.")))
            : Task.FromResult(Runs[runId]);

    /// <inheritdoc />
    public Task<RevertResult> RevertAsync(Guid runId, RevertOptions options)
    {
        RevertCount++;
        LastRevertRunId = runId;
        LastRevertOptions = options;
        return ThrowOnRevert ? Task.FromException<RevertResult>(new InvalidOperationException("revert failed")) : Task.FromResult(RevertResultToReturn);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<(JournalSummary Run, RevertResult Result)>> RevertEverythingAsync(RevertOptions options)
    {
        RevertEverythingCount++;
        return ThrowOnRevert
            ? Task.FromException<IReadOnlyList<(JournalSummary Run, RevertResult Result)>>(new InvalidOperationException("revert failed"))
            : Task.FromResult(EverythingToReturn);
    }
}
