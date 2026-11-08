using OpenTheWindows.Core.Persistence;

namespace OpenTheWindows.Cli.Commands;

/// <summary>The <c>watch diff --json</c> result.</summary>
/// <param name="BaselineTakenAt">When the baseline compared against was taken.</param>
/// <param name="Changes">What was added, removed or changed since.</param>
internal sealed record WatchDiffJson(DateTimeOffset BaselineTakenAt, IReadOnlyList<WatchChangeJson> Changes)
{
    /// <summary>Projects a comparison.</summary>
    public static WatchDiffJson From(PersistenceComparison comparison) => new(
        comparison.Baseline.TakenAt,
        [.. comparison.Changes.Select(c => new WatchChangeJson(c.Change, c.Item.Kind, c.Item.Key, c.Before?.Detail, c.After?.Detail))]);
}
