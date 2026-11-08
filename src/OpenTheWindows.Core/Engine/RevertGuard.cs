using System.Globalization;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.Core.Engine;

/// <summary>
/// Decides whether reverting a run is safe to do without <c>--force</c>. A revert
/// writes the captured priors back, so it is refused when it would silently undo
/// later work: the run was already reverted, or a later run (not itself reverted)
/// changed one of the same settings. A run still marked in progress is never
/// reverted directly — crash recovery settles it first.
/// </summary>
internal static class RevertGuard
{
    /// <summary>Returns why the revert must not proceed, or <see langword="null"/> when it may.</summary>
    public static string? Check(JournalRecord original, IJournalStore store, bool force)
    {
        if (original.Result == RunState.InProgress)
        {
            return string.Create(CultureInfo.InvariantCulture,
                $"Run {original.RunId} did not finish; the next apply or revert recovers it automatically.");
        }

        if (force)
        {
            return null;
        }

        List<JournalSummary> history = [.. store.List().Where(s => s.Integrity is JournalIntegrity.Verified or JournalIntegrity.Unverified)];
        HashSet<Guid> reverted = [.. history
            .Where(s => s.RevertOf is not null && s.Result == RunState.Completed)
            .Select(s => s.RevertOf!.Value)];

        if (reverted.Contains(original.RunId))
        {
            return string.Create(CultureInfo.InvariantCulture,
                $"Run {original.RunId} was already reverted; pass --force to revert it again.");
        }

        HashSet<string> targets = Targets(original);
        HashSet<Guid> later = [.. history.Where(s => s.RunId != original.RunId && s.StartedAt > original.StartedAt).Select(s => s.RunId)];

        // A later run counts unless it was itself reverted, or it is the revert of another
        // later run (that revert only put back the state the original run left).
        JournalSummary? overlapping = history
            .Where(s => later.Contains(s.RunId)
                && s.Result is RunState.Completed or RunState.Failed
                && !reverted.Contains(s.RunId)
                && !(s.RevertOf is { } of && later.Contains(of)))
            .FirstOrDefault(s => Targets(store.Load(s.RunId)).Overlaps(targets));

        return overlapping is null
            ? null
            : string.Create(CultureInfo.InvariantCulture,
                $"Run {overlapping.RunId} ({overlapping.Profile}, {overlapping.StartedAt:u}) changed some of the same settings later; reverting would overwrite them. Revert that run first, or pass --force.");
    }

    private static HashSet<string> Targets(JournalRecord record)
        => [.. record.Entries
            .SelectMany(e => e.Actions)
            .Where(a => a.Result == ApplyState.Applied)
            .Select(a => string.Create(CultureInfo.InvariantCulture, $"{a.Sid}|{a.Target}"))];
}
