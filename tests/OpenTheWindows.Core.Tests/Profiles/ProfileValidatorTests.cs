using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Core.Tests.Profiles;

public sealed class ProfileValidatorTests
{
    private static IReadOnlyList<CatalogIssue> Validate(Profile profile, bool isBuiltIn = false)
        => ProfileValidator.Validate(profile, ProfileTestData.SampleCatalog(), isBuiltIn);

    [Fact]
    public void Unknown_include_id_is_an_error()
    {
        Profile profile = ProfileTestData.Make(include: [new TweakId("does.not.exist")]);

        Assert.Contains(Validate(profile), i => string.Equals(i.Rule, "include-unknown", StringComparison.Ordinal) && i.Severity == CatalogIssueSeverity.Error);
    }

    [Fact]
    public void Unknown_exclude_id_is_a_warning()
    {
        Profile profile = ProfileTestData.Make(exclude: [new TweakId("does.not.exist")]);

        IReadOnlyList<CatalogIssue> issues = Validate(profile);

        Assert.Contains(issues, i => string.Equals(i.Rule, "exclude-unknown", StringComparison.Ordinal) && i.Severity == CatalogIssueSeverity.Warning);
        Assert.DoesNotContain(issues, i => i.Severity == CatalogIssueSeverity.Error);
    }

    [Fact]
    public void Include_and_exclude_overlap_is_a_warning()
    {
        Profile profile = ProfileTestData.Make(
            include: [new TweakId("privacy.a.basic-safe")],
            exclude: [new TweakId("privacy.a.basic-safe")]);

        Assert.Contains(Validate(profile), i => string.Equals(i.Rule, "include-exclude-overlap", StringComparison.Ordinal));
    }

    [Fact]
    public void Including_a_deprecated_entry_is_a_warning()
    {
        Profile profile = ProfileTestData.Make(include: [new TweakId("privacy.g.basic-deprecated")]);

        Assert.Contains(Validate(profile), i => string.Equals(i.Rule, "include-deprecated", StringComparison.Ordinal));
    }

    [Fact]
    public void Built_in_profile_that_allows_breaking_is_an_error()
    {
        Profile profile = ProfileTestData.Make(options: ProfileTestData.Options(allowBreaking: true));

        Assert.Contains(Validate(profile, isBuiltIn: true),
            i => string.Equals(i.Rule, "builtin-allow-breaking", StringComparison.Ordinal) && i.Severity == CatalogIssueSeverity.Error);
    }

    [Fact]
    public void Built_in_profile_that_includes_a_breaking_entry_is_an_error()
    {
        Profile profile = ProfileTestData.Make(include: [new TweakId("privacy.d.strict-breaking")]);

        Assert.Contains(Validate(profile, isBuiltIn: true),
            i => string.Equals(i.Rule, "builtin-include-breaking", StringComparison.Ordinal) && i.Severity == CatalogIssueSeverity.Error);
    }

    [Fact]
    public void Custom_profile_may_include_a_breaking_entry()
    {
        Profile profile = ProfileTestData.Make(include: [new TweakId("privacy.d.strict-breaking")]);

        Assert.DoesNotContain(Validate(profile, isBuiltIn: false), i => i.Severity == CatalogIssueSeverity.Error);
    }

    [Fact]
    public void A_clean_profile_produces_no_issues()
    {
        Profile profile = ProfileTestData.Make(levels: ProfileTestData.Levels(privacy: Level.Basic));

        Assert.Empty(Validate(profile));
    }
}
