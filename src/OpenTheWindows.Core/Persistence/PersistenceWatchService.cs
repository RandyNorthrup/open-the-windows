using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Core.Persistence;

/// <summary>
/// The persistence watch: record every autostart (services, scheduled tasks, Run/RunOnce
/// values, Startup-folder items) as a baseline, then later list what was added, removed or
/// changed since — the places malware and unwanted installers hide to survive a reboot.
/// Read-only apart from the baseline file; nothing on the machine is changed.
/// </summary>
public sealed class PersistenceWatchService
{
    private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

    private readonly IPersistenceSource _source;
    private readonly IPersistenceSnapshotStore _store;
    private readonly IInteractiveUserResolver _users;
    private readonly TimeProvider _time;

    /// <summary>Creates the watch over the autostart source, the baseline store, the interactive-user resolver and the clock.</summary>
    public PersistenceWatchService(IPersistenceSource source, IPersistenceSnapshotStore store, IInteractiveUserResolver users, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(users);
        ArgumentNullException.ThrowIfNull(time);
        _source = source;
        _store = store;
        _users = users;
        _time = time;
    }

    /// <summary>Records every autostart now as the new baseline and returns it.</summary>
    public PersistenceSnapshot TakeSnapshot()
    {
        var snapshot = new PersistenceSnapshot(PersistenceSnapshot.CurrentSchemaVersion, _time.GetUtcNow(), Capture());
        _store.Save(snapshot);
        return snapshot;
    }

    /// <summary>
    /// Compares the autostarts now with the baseline. Returns <see langword="null"/> when no
    /// baseline has been taken; otherwise the baseline and the differences (empty when none).
    /// </summary>
    public PersistenceComparison? Compare()
    {
        PersistenceSnapshot? baseline = _store.Load();
        return baseline is null ? null : new PersistenceComparison(baseline, Diff(baseline.Items, Capture()));
    }

    /// <summary>The differences between two sets of autostarts, keyed by kind and key (case-insensitive), ordered by kind then key.</summary>
    public static IReadOnlyList<PersistenceChange> Diff(IReadOnlyList<PersistenceItem> before, IReadOnlyList<PersistenceItem> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        Dictionary<(PersistenceKind, string), PersistenceItem> was = Index(before);
        Dictionary<(PersistenceKind, string), PersistenceItem> now = Index(after);
        IEnumerable<PersistenceChange> added = now.Where(n => !was.ContainsKey(n.Key)).Select(n => new PersistenceChange(null, n.Value));
        IEnumerable<PersistenceChange> removed = was.Where(w => !now.ContainsKey(w.Key)).Select(w => new PersistenceChange(w.Value, null));
        IEnumerable<PersistenceChange> changed = now
            .Where(n => was.TryGetValue(n.Key, out PersistenceItem? old) && !string.Equals(old.Detail, n.Value.Detail, StringComparison.Ordinal))
            .Select(n => new PersistenceChange(was[n.Key], n.Value));
        return [.. added.Concat(removed).Concat(changed)
            .OrderBy(c => c.Item.Kind)
            .ThenBy(c => c.Item.Key, KeyComparer)];
    }

    private IReadOnlyList<PersistenceItem> Capture() => _source.Capture(_users.Resolve()?.Sid);

    // A duplicate key keeps the first item, so a source that reports one twice cannot fail the diff.
    private static Dictionary<(PersistenceKind, string), PersistenceItem> Index(IReadOnlyList<PersistenceItem> items)
    {
        var index = new Dictionary<(PersistenceKind, string), PersistenceItem>(new KindKeyComparer());
        foreach (PersistenceItem item in items)
        {
            index.TryAdd((item.Kind, item.Key), item);
        }

        return index;
    }

    private sealed class KindKeyComparer : IEqualityComparer<(PersistenceKind Kind, string Key)>
    {
        public bool Equals((PersistenceKind Kind, string Key) x, (PersistenceKind Kind, string Key) y)
            => x.Kind == y.Kind && KeyComparer.Equals(x.Key, y.Key);

        public int GetHashCode((PersistenceKind Kind, string Key) obj) => HashCode.Combine(obj.Kind, KeyComparer.GetHashCode(obj.Key));
    }
}
