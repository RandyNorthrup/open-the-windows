using OpenTheWindows.Core.Persistence;

namespace OpenTheWindows.Cli.Commands;

/// <summary>One change in <see cref="WatchDiffJson"/>.</summary>
/// <param name="Change">Added, removed or changed.</param>
/// <param name="Kind">The kind of autostart.</param>
/// <param name="Key">Its identity.</param>
/// <param name="Before">What it ran before (absent when added).</param>
/// <param name="After">What it runs now (absent when removed).</param>
internal sealed record WatchChangeJson(PersistenceChangeKind Change, PersistenceKind Kind, string Key, string? Before, string? After);
