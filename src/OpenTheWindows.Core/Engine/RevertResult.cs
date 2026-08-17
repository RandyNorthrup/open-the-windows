using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.Core.Engine;

/// <summary>The result of a revert run.</summary>
/// <param name="RunId">The new journaled run id for the revert (<see cref="Guid.Empty"/> for a what-if).</param>
/// <param name="RevertedRunId">The id of the run that was reverted.</param>
/// <param name="State">The revert run's overall state.</param>
/// <param name="Entries">The per-entry outcomes.</param>
public sealed record RevertResult(
    Guid RunId,
    Guid RevertedRunId,
    RunState State,
    IReadOnlyList<EntryOutcome> Entries);
