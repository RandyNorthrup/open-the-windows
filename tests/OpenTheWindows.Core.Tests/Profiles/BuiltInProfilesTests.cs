using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Core.Tests.Profiles;

public sealed class BuiltInProfilesTests
{
    private static readonly string[] ExpectedIds =
    [
        "developer",
        "enterprise-workstation",
        "gamer",
        "home",
        "kiosk-shared",
        "power-user",
        "privacy-max",
    ];

    private static TweakCatalog Catalog()
    {
        CatalogLoadResult result = CatalogLoader.LoadBuiltIn();
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(e => e.ToString())));
        return result.Catalog!;
    }

    [Fact]
    public void Built_in_ids_are_the_expected_seven()
    {
        Assert.Equal(ExpectedIds, ProfileLoader.BuiltInIds);
    }

    [Fact]
    public void All_built_in_profiles_load_without_errors()
    {
        IReadOnlyList<ProfileLoadResult> results = ProfileLoader.LoadBuiltIns();

        Assert.Equal(ExpectedIds.Length, results.Count);
        foreach (ProfileLoadResult result in results)
        {
            Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(e => e.ToString())));
        }
    }

    [Fact]
    public void Every_built_in_passes_the_structural_validator()
    {
        TweakCatalog catalog = Catalog();

        foreach (ProfileLoadResult result in ProfileLoader.LoadBuiltIns())
        {
            IReadOnlyList<CatalogIssue> issues = ProfileValidator.Validate(result.Profile!, catalog, isBuiltIn: true);
            Assert.DoesNotContain(issues, i => i.Severity == CatalogIssueSeverity.Error);
        }
    }

    [Fact]
    public void No_built_in_profile_allows_breaking()
    {
        foreach (ProfileLoadResult result in ProfileLoader.LoadBuiltIns())
        {
            Assert.False(result.Profile!.Options.AllowBreaking, result.Profile.Id);
        }
    }

    [Fact]
    public void No_built_in_profile_resolves_to_a_breaking_entry()
    {
        TweakCatalog catalog = Catalog();
        int total = 0;

        foreach (ProfileLoadResult result in ProfileLoader.LoadBuiltIns())
        {
            IReadOnlyList<TweakDefinition> entries = ProfileResolver.Resolve(result.Profile!, catalog, ProfileTestData.ProFacts);
            Assert.DoesNotContain(entries, e => e.Risk == RiskTier.Breaking);
            total += entries.Count;
        }

        Assert.True(total > 0, "Built-in profiles should collectively select at least one entry on a Pro machine.");
    }

    [Fact]
    public void No_built_in_profile_resolves_to_a_conflicting_pair()
    {
        // The resolver's conflict backstop must leave every built-in profile with an applicable
        // plan; two selected entries that conflict would make the engine reject the whole run.
        TweakCatalog catalog = Catalog();

        foreach (ProfileLoadResult result in ProfileLoader.LoadBuiltIns())
        {
            IReadOnlyList<TweakDefinition> entries = ProfileResolver.Resolve(result.Profile!, catalog, ProfileTestData.ProFacts);
            HashSet<TweakId> present = [.. entries.Select(e => e.Id)];
            foreach (TweakDefinition entry in entries)
            {
                foreach (TweakId conflict in entry.ConflictsWith)
                {
                    Assert.False(
                        present.Contains(conflict),
                        $"{result.Profile!.Id}: {entry.Id.Value} conflicts with also-selected {conflict.Value}");
                }
            }
        }
    }

    [Fact]
    public void Balanced_profiles_resolve_to_the_intended_update_defaults()
    {
        // defer-30-days is the single Balanced feature-update default; the Delivery Optimization
        // mode is per-profile (LAN-only for the managed/shared machines, HTTP-only otherwise),
        // and the conflicting alternatives never come along.
        TweakCatalog catalog = Catalog();
        (string Profile, string DeliveryOptimization)[] expected =
        [
            ("enterprise-workstation", "updates.delivery-optimization.lan-only"),
            ("kiosk-shared", "updates.delivery-optimization.lan-only"),
            ("privacy-max", "updates.delivery-optimization.http-only"),
            ("developer", "updates.delivery-optimization.http-only"),
            ("power-user", "updates.delivery-optimization.http-only"),
        ];

        foreach ((string id, string deliveryOptimization) in expected)
        {
            Profile profile = ProfileLoader.TryLoadBuiltIn(id)!.Profile!;
            HashSet<string> ids = [.. ProfileResolver.Resolve(profile, catalog, ProfileTestData.ProFacts).Select(e => e.Id.Value)];

            Assert.Contains("updates.feature-updates.defer-30-days", ids);
            Assert.Contains(deliveryOptimization, ids);
            Assert.DoesNotContain("updates.feature-updates.defer-90-days", ids);
            Assert.DoesNotContain("updates.feature-release.pin-to-25h2", ids);
            string otherMode = deliveryOptimization.EndsWith("lan-only", StringComparison.Ordinal)
                ? "updates.delivery-optimization.http-only"
                : "updates.delivery-optimization.lan-only";
            Assert.DoesNotContain(otherMode, ids);
        }
    }

    [Fact]
    public void TryLoadBuiltIn_finds_by_id_and_returns_null_for_unknown()
    {
        Assert.Equal("home", ProfileLoader.TryLoadBuiltIn("home")?.Profile?.Id);
        Assert.Null(ProfileLoader.TryLoadBuiltIn("no-such-profile"));
    }
}
