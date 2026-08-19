using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Audit;

/// <summary>
/// Structural rules for a baseline that the JSON Schema cannot express: every
/// referenced tweak id must exist in the catalogue, every referenced health-check
/// id must be a known probe id, every rule needs at least one https source, and
/// rule ids are unique within the baseline. CIS rules carry only a section id and
/// a paraphrased title — the model has no field for verbatim benchmark text, so
/// that ADR 0002 constraint is structural rather than checked here.
/// </summary>
public static class BaselineValidator
{
    /// <summary>Longest rule title the HTML/CSV reports render comfortably.</summary>
    public const int MaxTitleLength = 200;

    /// <summary>
    /// Validates <paramref name="baseline"/> against <paramref name="catalog"/> and
    /// the set of known health-check ids. Returns one <see cref="CatalogIssue"/> per
    /// problem; an empty result means the baseline is structurally valid.
    /// </summary>
    /// <param name="baseline">The parsed baseline.</param>
    /// <param name="source">Source name (logical resource name or file path) for the findings.</param>
    /// <param name="catalog">The loaded catalogue, used to resolve tweak ids.</param>
    /// <param name="knownHealthCheckIds">The ids a health probe can emit (<see cref="Abstractions.HealthCheckIds.All"/>).</param>
    public static IReadOnlyList<CatalogIssue> Validate(
        Baseline baseline,
        string source,
        TweakCatalog catalog,
        IReadOnlySet<string> knownHealthCheckIds)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(knownHealthCheckIds);

        List<CatalogIssue> issues = [];

        if (string.IsNullOrWhiteSpace(baseline.Title))
        {
            issues.Add(Error(source, baseline.Id, "title-required", "Baseline title is required."));
        }

        if (baseline.Rules.Count == 0)
        {
            issues.Add(Error(source, baseline.Id, "rules-required", "A baseline needs at least one rule."));
        }

        foreach (string duplicateId in baseline.Rules.GroupBy(r => r.RuleId, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key))
        {
            issues.Add(Error(source, duplicateId, "unique-rule-id", $"Duplicate ruleId '{duplicateId}'."));
        }

        foreach (BaselineRule rule in baseline.Rules)
        {
            ValidateRule(rule, source, catalog, knownHealthCheckIds, issues);
        }

        return issues;
    }

    private static void ValidateRule(
        BaselineRule rule,
        string source,
        TweakCatalog catalog,
        IReadOnlySet<string> knownHealthCheckIds,
        List<CatalogIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(rule.Title))
        {
            issues.Add(Error(source, rule.RuleId, "title-required", "Rule title is required."));
        }
        else if (rule.Title.Length > MaxTitleLength)
        {
            issues.Add(Error(source, rule.RuleId, "title-length", $"Rule title is longer than {MaxTitleLength} characters."));
        }

        if (rule.Sources.Count == 0)
        {
            issues.Add(Error(source, rule.RuleId, "sources-required", "Each rule needs at least one documentation source URL."));
        }

        foreach (Uri uri in rule.Sources)
        {
            if (!uri.IsAbsoluteUri || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            {
                issues.Add(Error(source, rule.RuleId, "source-https", $"Source '{uri}' must be an absolute https URL."));
            }
        }

        foreach (TweakId tweakId in rule.TweakIds)
        {
            if (!catalog.TryGet(tweakId, out _))
            {
                issues.Add(Error(source, rule.RuleId, "unknown-tweak", $"tweakIds references unknown catalogue id '{tweakId}'."));
            }
        }

        if (rule.CheckOnly is { } check && !knownHealthCheckIds.Contains(check.HealthCheckId))
        {
            issues.Add(Error(source, rule.RuleId, "unknown-health-check", $"checkOnly references unknown health-check id '{check.HealthCheckId}'."));
        }
    }

    private static CatalogIssue Error(string source, string ruleId, string rule, string message)
        => new(CatalogIssueSeverity.Error, source, ruleId, rule, message);
}
