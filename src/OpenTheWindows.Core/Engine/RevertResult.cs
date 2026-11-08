using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.Core.Engine;

/// <summary>The result of a revert run.</summary>
/// <param name="RunId">The new journaled run id for the revert (<see cref="Guid.Empty"/> for a what-if).</param>
/// <param name="RevertedRunId">The id of the run that was reverted.</param>
/// <param name="State">The revert run's overall state.</param>
/// <param name="Entries">The per-entry outcomes.</param>
/// <param name="AlreadyReverted">Set when nothing was done because the run (or the requested entries) had already been reverted.</param>
public sealed record RevertResult(
    Guid RunId,
    Guid RevertedRunId,
    RunState State,
    IReadOnlyList<EntryOutcome> Entries,
    bool AlreadyReverted = false);
