using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Core.Tests.Profiles;

public sealed class ProfileResolverTests
{
    private static IReadOnlyList<string> ResolveIds(Profile profile)
        => [.. ProfileResolver.Resolve(profile, ProfileTestData.SampleCatalog(), ProfileTestData.ProFacts)
            .Select(e => e.Id.Value)];

    private static HashSet<string> ResolveSet(Profile profile)
        => ProfileResolver.Resolve(profile, ProfileTestData.SampleCatalog(), ProfileTestData.ProFacts)
            .Select(e => e.Id.Value)
            .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Level_dial_selects_entries_at_or_below_the_level()
    {
        Profile profile = ProfileTestData.Make(levels: ProfileTestData.Levels(privacy: Level.Basic));

        // At the Basic dial only the Basic Verified entry qualifies. Higher levels,
        // Draft, Deprecated and Enterprise-only entries are all excluded here.
        Assert.Equal(["privacy.a.basic-safe"], ResolveIds(profile));
    }

    [Fact]
    public void Higher_level_includes_lower_level_entries()
    {
        Profile profile = ProfileTestData.Make(levels: ProfileTestData.Levels(privacy: Level.Balanced));

        Assert.Equal(["privacy.a.basic-safe", "privacy.b.balanced-safe"], ResolveIds(profile));
    }

    [Fact]
    public void Include_draft_lets_level_dials_select_draft_entries()
    {
        Profile profile = ProfileTestData.Make(
            levels: ProfileTestData.Levels(privacy: Level.Basic),
            includeDraft: true);

        Assert.Contains("privacy.e.basic-draft", ResolveSet(profile));
    }

    [Fact]
    public void Advanced_entries_are_gated_until_the_option_allows_them()
    {
        var levels = ProfileTestData.Levels(privacy: Level.Strict);

        Assert.DoesNotContain("privacy.c.strict-advanced",
            ResolveSet(ProfileTestData.Make(levels: levels, options: ProfileTestData.Options())));
        Assert.Contains("privacy.c.strict-advanced",
            ResolveSet(ProfileTestData.Make(levels: levels, options: ProfileTestData.Options(allowAdvanced: true))));
    }

    [Fact]
    public void Breaking_entries_are_gated_until_the_option_allows_them()
    {
        var levels = ProfileTestData.Levels(privacy: Level.Strict);

        Assert.DoesNotContain("privacy.d.strict-breaking",
            ResolveSet(ProfileTestData.Make(levels: levels, options: ProfileTestData.Options(allowAdvanced: true))));
        Assert.Contains("privacy.d.strict-breaking",
            ResolveSet(ProfileTestData.Make(levels: levels, options: ProfileTestData.Options(allowAdvanced: true, allowBreaking: true))));
    }

    [Fact]
    public void Include_forces_an_entry_in_bypassing_level_and_risk_gates()
    {
        // Privacy dial is Off and Advanced is not allowed, yet an explicit include wins.
        Profile profile = ProfileTestData.Make(include: [new TweakId("privacy.c.strict-advanced")]);

        Assert.Equal(["privacy.c.strict-advanced"], ResolveIds(profile));
    }

    [Fact]
    public void Include_forces_a_draft_entry_in_even_when_include_draft_is_false()
    {
        Profile profile = ProfileTestData.Make(include: [new TweakId("privacy.e.basic-draft")]);

        Assert.Contains("privacy.e.basic-draft", ResolveSet(profile));
    }

    [Fact]
    public void Include_never_pulls_in_a_deprecated_entry()
    {
        Profile profile = ProfileTestData.Make(include: [new TweakId("privacy.g.basic-deprecated")]);

        Assert.Empty(ResolveIds(profile));
    }

    [Fact]
    public void Exclude_removes_a_level_selected_entry()
    {
        Profile profile = ProfileTestData.Make(
            levels: ProfileTestData.Levels(privacy: Level.Basic),
            exclude: [new TweakId("privacy.a.basic-safe")]);

        Assert.Empty(ResolveIds(profile));
    }

    [Fact]
    public void Exclude_wins_over_include()
    {
        Profile profile = ProfileTestData.Make(
            include: [new TweakId("privacy.b.balanced-safe")],
            exclude: [new TweakId("privacy.b.balanced-safe")]);

        Assert.Empty(ResolveIds(profile));
    }

    [Fact]
    public void Entries_that_do_not_apply_to_the_machine_are_dropped()
    {
        // The Enterprise-only entry is Basic/Verified but must not resolve on Pro.
        Profile profile = ProfileTestData.Make(levels: ProfileTestData.Levels(privacy: Level.Basic));

        Assert.DoesNotContain("privacy.h.enterprise-only", ResolveSet(profile));
    }

    [Fact]
    public void Result_is_in_catalogue_load_order()
    {
        Profile profile = ProfileTestData.Make(
            levels: ProfileTestData.Levels(privacy: Level.Strict, security: Level.Basic),
            options: ProfileTestData.Options(allowAdvanced: true, allowBreaking: true));

        Assert.Equal(
            [
                "privacy.a.basic-safe",
                "privacy.b.balanced-safe",
                "privacy.c.strict-advanced",
                "privacy.d.strict-breaking",
                "security.f.basic-safe",
            ],
            ResolveIds(profile));
    }
}
