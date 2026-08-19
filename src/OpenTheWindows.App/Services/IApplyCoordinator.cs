using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.App.Services;

/// <summary>
/// The GUI's gateway to the apply engine: a synchronous what-if plan, an
/// asynchronous transactional apply (run off the UI thread), and a manual
/// Explorer restart. Abstracted so view models are testable with a fake engine.
/// </summary>
internal interface IApplyCoordinator
{
    /// <summary>Plans the run without changing anything (the what-if diff).</summary>
    PlanResult Preview(IReadOnlyList<TweakDefinition> entries, ApplyOptions options);

    /// <summary>Applies the run transactionally on a background thread.</summary>
    Task<ApplyResult> ApplyAsync(IReadOnlyList<TweakDefinition> entries, ApplyOptions options);

    /// <summary>Restarts Explorer for the interactive user's session, off the UI thread.</summary>
    Task RestartExplorerAsync();
}
