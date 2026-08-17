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
    public void TryLoadBuiltIn_finds_by_id_and_returns_null_for_unknown()
    {
        Assert.Equal("home", ProfileLoader.TryLoadBuiltIn("home")?.Profile?.Id);
        Assert.Null(ProfileLoader.TryLoadBuiltIn("no-such-profile"));
    }
}
