using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Profiles;

/// <summary>
/// Turns a <see cref="Profile"/> into the concrete, ordered set of catalogue
/// entries it selects for a machine. Deterministic and side-effect free.
/// </summary>
/// <remarks>
/// Resolution order:
/// <list type="number">
/// <item>Per category, take every entry the level dial selects
/// (<see cref="TweakCatalog.Select"/>), dropping Advanced/Breaking entries unless
/// the profile's <see cref="ProfileOptions"/> allow them.</item>
/// <item>Force in every <see cref="Profile.Include"/> id that exists and is not
/// Deprecated — includes bypass the level and risk gates (an explicit opt-in).</item>
/// <item>Remove every <see cref="Profile.Exclude"/> id — exclude always wins.</item>
/// <item>Keep only entries whose <see cref="TweakDefinition.AppliesTo"/> matches
/// the machine.</item>
/// <item>Drop mutually-conflicting entries so the result is always applicable: within
/// a conflict group the survivor is an explicitly included entry, else the higher level
/// (the more aggressive choice the dial reached), else the earlier in catalogue order.</item>
/// </list>
/// The result is returned in catalogue load order.
/// </remarks>
public static class ProfileResolver
{
    /// <summary>Resolves the entries <paramref name="profile"/> selects on <paramref name="facts"/>.</summary>
    public static IReadOnlyList<TweakDefinition> Resolve(Profile profile, TweakCatalog catalog, OperatingSystemFacts facts)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(facts);

        HashSet<TweakId> selected = [];

        foreach (Category category in Enum.GetValues<Category>())
        {
            IEnumerable<TweakDefinition> byLevel = catalog
                .Select(category, profile.LevelFor(category), profile.IncludeDraft)
                .Where(entry => IsRiskAllowed(entry.Risk, profile.Options));
            foreach (TweakDefinition entry in byLevel)
            {
                selected.Add(entry.Id);
            }
        }

        foreach (TweakId id in profile.Include)
        {
            if (catalog.TryGet(id, out TweakDefinition? entry) && entry.Status != TweakStatus.Deprecated)
            {
                selected.Add(id);
            }
        }

        foreach (TweakId id in profile.Exclude)
        {
            selected.Remove(id);
        }

        List<TweakDefinition> resolved =
            [.. catalog.Entries.Where(e => selected.Contains(e.Id) && e.AppliesTo.Matches(facts))];
        ResolveConflicts(resolved, [.. profile.Include]);
        return resolved;
    }

    private static bool IsRiskAllowed(RiskTier risk, ProfileOptions options)
        => risk switch
        {
            RiskTier.Breaking => options.AllowBreaking,
            RiskTier.Advanced => options.AllowAdvanced,
            _ => true,
        };

    /// <summary>
    /// Removes mutually-conflicting entries in place until none remain, so a profile always
    /// yields an applicable plan (the engine would otherwise reject the run). <paramref name="entries"/>
    /// is in catalogue order; the survivor of each conflict is decided by <see cref="LoserIndex"/>.
    /// </summary>
    private static void ResolveConflicts(List<TweakDefinition> entries, HashSet<TweakId> included)
    {
        int loser = FindLoserIndex(entries, included);
        while (loser >= 0)
        {
            entries.RemoveAt(loser);
            loser = FindLoserIndex(entries, included);
        }
    }

    /// <summary>The index of the entry to drop from the first conflicting pair found, or -1 when there is no conflict.</summary>
    private static int FindLoserIndex(List<TweakDefinition> entries, HashSet<TweakId> included)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            for (int j = i + 1; j < entries.Count; j++)
            {
                if (Conflicts(entries[i], entries[j]))
                {
                    return LoserIndex(entries[i], entries[j], i, j, included);
                }
            }
        }

        return -1;
    }

    /// <summary>Whether two entries conflict (in either direction — <c>conflictsWith</c> need not be symmetric).</summary>
    private static bool Conflicts(TweakDefinition a, TweakDefinition b)
        => a.ConflictsWith.Contains(b.Id) || b.ConflictsWith.Contains(a.Id);

    /// <summary>
    /// Which of two conflicting entries loses: an explicitly included entry always wins over a
    /// level-selected one; otherwise the lower level loses to the higher (more aggressive) one;
    /// otherwise the later in catalogue order (<paramref name="j"/>) loses. Since <paramref name="i"/>
    /// precedes <paramref name="j"/>, keeping <paramref name="i"/> keeps the earlier entry.
    /// </summary>
    private static int LoserIndex(TweakDefinition a, TweakDefinition b, int i, int j, HashSet<TweakId> included)
    {
        bool aIncluded = included.Contains(a.Id);
        bool bIncluded = included.Contains(b.Id);
        if (aIncluded != bIncluded)
        {
            return aIncluded ? j : i;
        }

        if (a.Level != b.Level)
        {
            return a.Level > b.Level ? j : i;
        }

        return j;
    }
}
