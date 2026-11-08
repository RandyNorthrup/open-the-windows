using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.App.Services;

/// <summary>
/// The GUI's gateway to the apply engine: the what-if plan, a read-only scan, the
/// transactional apply, the run history with per-run detail, revert (whole run, one
/// entry, or everything) and a manual Explorer restart. Everything that reads or changes
/// the machine runs off the UI thread. Abstracted so view models are testable with a
/// fake engine.
/// </summary>
internal interface IApplyCoordinator
{
    /// <summary>Plans the run without changing anything (the what-if diff), on a background thread.</summary>
    Task<PlanResult> PreviewAsync(IReadOnlyList<TweakDefinition> entries, ApplyOptions options);

    /// <summary>Reads the current state of <paramref name="entries"/> (read-only), on a background thread.</summary>
    Task<ScanReport> ScanAsync(IReadOnlyList<TweakDefinition> entries);

    /// <summary>Applies the run transactionally on a background thread.</summary>
    Task<ApplyResult> ApplyAsync(IReadOnlyList<TweakDefinition> entries, ApplyOptions options);

    /// <summary>Restarts Explorer for the interactive user's session, off the UI thread.</summary>
    Task RestartExplorerAsync();

    /// <summary>The prior runs, newest first.</summary>
    IReadOnlyList<JournalSummary> History();

    /// <summary>Loads one run's journal (its entries and outcomes), on a background thread.</summary>
    Task<JournalRecord> LoadRunAsync(Guid runId);

    /// <summary>Reverts a prior run (or the entries named in <paramref name="options"/>), on a background thread.</summary>
    Task<RevertResult> RevertAsync(Guid runId, RevertOptions options);

    /// <summary>Undoes every change the product made, newest run first, on a background thread.</summary>
    Task<IReadOnlyList<(JournalSummary Run, RevertResult Result)>> RevertEverythingAsync(RevertOptions options);
}
