using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Catalog;

/// <summary>Validated, immutable set of catalogue entries with lookups.</summary>
public sealed class TweakCatalog
{
    private readonly Dictionary<TweakId, TweakDefinition> _byId;

    internal TweakCatalog(IReadOnlyList<TweakDefinition> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = entries;
        _byId = entries.ToDictionary(e => e.Id);
    }

    /// <summary>All entries in load order.</summary>
    public IReadOnlyList<TweakDefinition> Entries { get; }

    /// <summary>Number of entries.</summary>
    public int Count => Entries.Count;

    /// <summary>Looks up an entry by id.</summary>
    public bool TryGet(TweakId id, out TweakDefinition definition)
        => _byId.TryGetValue(id, out definition!);

    /// <summary>Returns the entry or throws <see cref="KeyNotFoundException"/>.</summary>
    public TweakDefinition Get(TweakId id)
        => _byId.TryGetValue(id, out TweakDefinition? definition)
            ? definition
            : throw new KeyNotFoundException($"Catalogue has no entry '{id}'.");

    /// <summary>Entries in a category, load order.</summary>
    public IEnumerable<TweakDefinition> InCategory(Category category)
        => Entries.Where(e => e.Category == category);

    /// <summary>
    /// Entries a profile at <paramref name="level"/> would select for
    /// <paramref name="category"/>: level at or below the dial, not Off, not
    /// deprecated. Draft entries are included only when <paramref name="includeDraft"/>.
    /// </summary>
    public IEnumerable<TweakDefinition> Select(Category category, Level level, bool includeDraft)
        => level == Level.Off
            ? []
            : Entries.Where(e => e.Category == category
                && e.Level != Level.Off
                && e.Level <= level
                && e.Status != TweakStatus.Deprecated
                && (includeDraft || e.Status == TweakStatus.Verified));

    /// <summary>Entries whose <see cref="Applicability"/> matches the machine.</summary>
    public IEnumerable<TweakDefinition> ApplicableTo(OperatingSystemFacts facts)
        => Entries.Where(e => e.AppliesTo.Matches(facts));
}
