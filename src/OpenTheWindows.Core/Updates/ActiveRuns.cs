using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.Core.Updates;

/// <summary>
/// The completed, not-yet-reverted runs a guardrail recorded under one profile name
/// (every pause, or every target), newest first — what "undo" must revert. Only a revert
/// that completed ends a run; one that failed leaves it in force, so the next undo retries.
/// </summary>
internal static class ActiveRuns
{
    public static IReadOnlyList<JournalSummary> For(IReadOnlyList<JournalSummary> history, string profileName)
    {
        HashSet<Guid> reverted = [.. history.Where(h => h.RevertOf is not null && h.Result == RunState.Completed).Select(h => h.RevertOf!.Value)];
        return
        [
            .. history
                .Where(h => string.Equals(h.Profile, profileName, StringComparison.Ordinal)
                    && h.Result == RunState.Completed
                    && h.RevertOf is null
                    && !reverted.Contains(h.RunId))
                .OrderByDescending(h => h.StartedAt),
        ];
    }
}
