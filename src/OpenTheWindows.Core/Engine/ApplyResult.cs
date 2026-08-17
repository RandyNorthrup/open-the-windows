using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Engine;

/// <summary>The result of an apply run.</summary>
/// <param name="RunId">The journaled run id (<see cref="Guid.Empty"/> for a what-if or a rejected plan).</param>
/// <param name="State">The run's overall state.</param>
/// <param name="Requires">The aggregate restart requirement over applied entries.</param>
/// <param name="RestorePoint">The restore-point outcome for the run.</param>
/// <param name="Preflight">The pre-flight report; blocking issues mean the run did not proceed.</param>
/// <param name="Plan">The plan the run executed (or would have executed for a what-if / rejected plan).</param>
/// <param name="Entries">The per-entry outcomes (empty for a what-if or a rejected plan).</param>
public sealed record ApplyResult(
    Guid RunId,
    RunState State,
    RestartRequirement Requires,
    RestorePointResult RestorePoint,
    PreflightReport Preflight,
    PlanResult Plan,
    IReadOnlyList<EntryOutcome> Entries);
