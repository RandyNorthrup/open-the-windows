using System.Globalization;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.Core.Engine;

/// <summary>
/// Decides what of a run is safe to revert without <c>--force</c>. A revert writes the
/// captured priors back, so it skips entries an earlier revert already undid (refusing
/// when nothing is left), and refuses when a later run (not itself reverted) changed one
/// of the same settings. A run still marked in progress is never reverted directly —
/// crash recovery settles it first.
/// </summary>
internal static class RevertGuard
{
    /// <summary>
    /// Returns why the revert must not proceed (or <see langword="null"/>), whether that is
    /// because everything requested was already reverted, and the requested entries that
    /// remain to be reverted.
    /// </summary>
    public static (string? Refusal, bool AlreadyReverted, List<JournalEntry> Remaining) Check(
        JournalRecord original, List<JournalEntry> requested, IJournalStore store, bool force)
    {
        if (original.Result == RunState.InProgress)
        {
            return (string.Create(CultureInfo.InvariantCulture,
                $"Run {original.RunId} did not finish; the next apply or revert recovers it automatically."), false, requested);
        }

        if (original.RevertOf is { } undone)
        {
            // Reverting a revert would re-apply the original run's changes without its plan, gates or
            // confirmations; that is what apply is for.
            return (string.Create(CultureInfo.InvariantCulture,
                $"Run {original.RunId} is itself a revert of run {undone}; reverting it would re-apply those changes. Apply the profile again instead."), false, requested);
        }

        if (force)
        {
            return (null, false, requested);
        }

        List<JournalSummary> history = [.. store.List().Where(s => s.Integrity is JournalIntegrity.Verified or JournalIntegrity.Unverified)];
        HashSet<string> alreadyReverted = RevertedEntries(original, history, store);
        List<JournalEntry> remaining = [.. requested.Where(e => !alreadyReverted.Contains(e.Id))];
        if (remaining.Count == 0)
        {
            return (string.Create(CultureInfo.InvariantCulture,
                $"Run {original.RunId} was already reverted; pass --force to revert it again."), true, requested);
        }

        string? overlap = LaterOverlap(original, remaining, history, store);
        return (overlap, false, remaining);
    }

    /// <summary>The ids of the run's entries that completed reverts have already undone.</summary>
    private static HashSet<string> RevertedEntries(JournalRecord original, List<JournalSummary> history, IJournalStore store)
        => new(
            history
                .Where(s => s.RevertOf == original.RunId && s.Result == RunState.Completed)
                .SelectMany(s => store.Load(s.RunId).Entries.Select(e => e.Id)),
            StringComparer.Ordinal);

    private static string? LaterOverlap(JournalRecord original, List<JournalEntry> remaining, List<JournalSummary> history, IJournalStore store)
    {
        HashSet<Guid> reverted = [.. history
            .Where(s => s.RevertOf is not null && s.Result == RunState.Completed)
            .Select(s => s.RevertOf!.Value)];
        HashSet<string> targets = Targets(remaining);
        HashSet<Guid> later = [.. history.Where(s => s.RunId != original.RunId && s.StartedAt > original.StartedAt).Select(s => s.RunId)];

        // A later run counts unless it was itself reverted, or it is a revert of this run or of
        // another later run (such a revert only put back state this run or a later one left).
        JournalSummary? overlapping = history
            .Where(s => later.Contains(s.RunId)
                && s.Result is RunState.Completed or RunState.Failed
                && !reverted.Contains(s.RunId)
                && !(s.RevertOf is { } of && (of == original.RunId || later.Contains(of))))
            .FirstOrDefault(s => Targets(store.Load(s.RunId).Entries).Overlaps(targets));

        return overlapping is null
            ? null
            : string.Create(CultureInfo.InvariantCulture,
                $"Run {overlapping.RunId} ({overlapping.Profile}, {overlapping.StartedAt:u}) changed some of the same settings later; reverting would overwrite them. Revert that run first, or pass --force.");
    }

    private static HashSet<string> Targets(IEnumerable<JournalEntry> entries)
        => [.. entries
            .SelectMany(e => e.Actions)
            .Where(a => a.Result == ApplyState.Applied)
            .Select(a => string.Create(CultureInfo.InvariantCulture, $"{a.Sid}|{a.Target}"))];
}
