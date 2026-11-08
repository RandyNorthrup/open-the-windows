using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Profiles;

/// <summary>
/// Turns a scan of this machine into a profile that reproduces it elsewhere: every
/// catalogue entry already in its desired state here is named in <c>include</c>, with
/// every level dial off, so applying the captured profile to another PC changes exactly
/// what was found on this one. Breaking-risk entries are never captured (a capture must
/// not carry a change that needs a typed confirmation), and the profile allows Advanced
/// entries only when it includes one. Its scope is the signed-in user (machine-wide entries
/// apply regardless; per-user ones go to whoever applies it).
/// </summary>
public static class ProfileCapture
{
    /// <summary>Builds the captured profile from <paramref name="report"/>.</summary>
    /// <param name="report">A scan over the entries to consider.</param>
    /// <param name="catalog">The catalogue the scan ran against (for each entry's scope and risk).</param>
    /// <param name="id">The new profile's kebab-case id.</param>
    /// <param name="name">The new profile's display name.</param>
    public static Profile FromScan(ScanReport report, TweakCatalog catalog, string id, string name)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        List<TweakDefinition> captured = [.. report.Results
            .Where(r => r.State == ComplianceState.Compliant && r.Risk < RiskTier.Breaking)
            .Select(r => catalog.Get(r.Id))];

        return new Profile(
            Profile.CurrentSchemaVersion,
            id,
            name,
            $"Captured from {report.Os.EditionId} build {report.Os.Build} on {report.At.UtcDateTime:yyyy-MM-dd}: the {captured.Count} catalogue settings that were already in place.",
            Enum.GetValues<Category>().ToDictionary(category => category, _ => Level.Off),
            [.. captured.Select(e => e.Id)],
            [],
            IncludeDraft: false,
            Scope.User,
            new ProfileOptions(RestorePoint: true, RestartExplorer: false, captured.Exists(e => e.Risk == RiskTier.Advanced), AllowBreaking: false),
            Applicability.Any);
    }
}
