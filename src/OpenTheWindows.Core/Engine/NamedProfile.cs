using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Engine;

/// <summary>
/// The built-in named level sets accepted by <c>otw scan --profile</c> for M2:
/// every category set to one level. A JSON profile format (per-category levels
/// and per-tweak overrides) arrives in M5; until then only these names are
/// accepted.
/// </summary>
public static class NamedProfile
{
    private static readonly Dictionary<string, Level> LevelsByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["basic"] = Level.Basic,
        ["balanced"] = Level.Balanced,
        ["strict"] = Level.Strict,
        ["paranoid"] = Level.Paranoid,
    };

    /// <summary>The accepted profile names, in ascending level order.</summary>
    public static IReadOnlyList<string> Names { get; } = ["basic", "balanced", "strict", "paranoid"];

    /// <summary>Resolves a profile name to its level (case-insensitive).</summary>
    public static bool TryResolve(string name, out Level level)
    {
        ArgumentNullException.ThrowIfNull(name);
        return LevelsByName.TryGetValue(name, out level);
    }

    /// <summary>
    /// Selects the catalogue entries a named profile applies: every entry whose
    /// declared level is on (not <see cref="Level.Off"/>) and at or below
    /// <paramref name="level"/>. Deprecated entries are never selected; draft
    /// entries are excluded unless <paramref name="includeDraft"/> is set.
    /// </summary>
    public static IReadOnlyList<TweakDefinition> Select(TweakCatalog catalog, Level level, bool includeDraft)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return
        [
            .. catalog.Entries.Where(entry =>
                entry.Level != Level.Off
                && entry.Level <= level
                && entry.Status != TweakStatus.Deprecated
                && (includeDraft || entry.Status == TweakStatus.Verified)),
        ];
    }
}
