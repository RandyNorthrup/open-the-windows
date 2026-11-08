namespace OpenTheWindows.Core.Persistence;

/// <summary>How one autostart differs between the baseline and now: added (no <see cref="Before"/>), removed (no <see cref="After"/>), or changed.</summary>
/// <param name="Before">The item in the baseline, or <see langword="null"/> when it is new.</param>
/// <param name="After">The item now, or <see langword="null"/> when it is gone.</param>
public sealed record PersistenceChange(PersistenceItem? Before, PersistenceItem? After)
{
    /// <summary>Whether the item is new, gone, or changed.</summary>
    public PersistenceChangeKind Change => (Before, After) switch
    {
        (null, _) => PersistenceChangeKind.Added,
        (_, null) => PersistenceChangeKind.Removed,
        _ => PersistenceChangeKind.Changed,
    };

    /// <summary>The item the change is about (the current one when it still exists).</summary>
    public PersistenceItem Item => After ?? Before!;
}
