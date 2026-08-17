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
/// the machine, returned in catalogue load order.</item>
/// </list>
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

        return [.. catalog.Entries.Where(e => selected.Contains(e.Id) && e.AppliesTo.Matches(facts))];
    }

    private static bool IsRiskAllowed(RiskTier risk, ProfileOptions options)
        => risk switch
        {
            RiskTier.Breaking => options.AllowBreaking,
            RiskTier.Advanced => options.AllowAdvanced,
            _ => true,
        };
}
