using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Profiles;

/// <summary>
/// Structural profile checks that need the catalogue and so cannot live in the
/// JSON Schema: include/exclude ids must exist, the two lists must not overlap
/// or reference retired entries, and a built-in profile must never be able to
/// apply a <see cref="RiskTier.Breaking"/> entry.
/// </summary>
public static class ProfileValidator
{
    /// <summary>
    /// Validates <paramref name="profile"/> against <paramref name="catalog"/>.
    /// Set <paramref name="isBuiltIn"/> for a profile that ships in the product;
    /// built-ins get the stricter no-Breaking rules.
    /// </summary>
    public static IReadOnlyList<CatalogIssue> Validate(Profile profile, TweakCatalog catalog, bool isBuiltIn)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(catalog);

        List<CatalogIssue> issues = [];
        string source = profile.Id;

        foreach (TweakId id in profile.Include)
        {
            if (!catalog.TryGet(id, out TweakDefinition? entry))
            {
                issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, source, id.Value, "include-unknown",
                    $"include references unknown entry '{id}'."));
                continue;
            }

            if (entry.Status == TweakStatus.Deprecated)
            {
                issues.Add(new CatalogIssue(CatalogIssueSeverity.Warning, source, id.Value, "include-deprecated",
                    $"include references deprecated entry '{id}'; it will be ignored when the profile is applied."));
            }

            if (isBuiltIn && entry.Risk == RiskTier.Breaking)
            {
                issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, source, id.Value, "builtin-include-breaking",
                    $"built-in profile includes Breaking-risk entry '{id}'; built-in profiles must never apply Breaking entries."));
            }
        }

        foreach (TweakId id in profile.Exclude)
        {
            if (!catalog.TryGet(id, out _))
            {
                issues.Add(new CatalogIssue(CatalogIssueSeverity.Warning, source, id.Value, "exclude-unknown",
                    $"exclude references unknown entry '{id}'; it has no effect."));
            }
        }

        foreach (TweakId id in profile.Include.Intersect(profile.Exclude))
        {
            issues.Add(new CatalogIssue(CatalogIssueSeverity.Warning, source, id.Value, "include-exclude-overlap",
                $"'{id}' is in both include and exclude; exclude wins and the entry is not applied."));
        }

        if (isBuiltIn && profile.Options.AllowBreaking)
        {
            issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, source, "/options/allowBreaking", "builtin-allow-breaking",
                "built-in profile sets allowBreaking; built-in profiles must never allow Breaking-risk entries."));
        }

        return issues;
    }
}
