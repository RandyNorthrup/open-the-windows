using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;
using static OpenTheWindows.Core.Tests.Catalog.CatalogTestData;

namespace OpenTheWindows.Core.Tests.Catalog;

public sealed class TweakCatalogTests
{
    private static readonly TweakDefinition BasicVerified =
        ValidEntry("privacy.basic.verified") with { Level = Level.Basic, Status = TweakStatus.Verified };

    private static readonly TweakDefinition BalancedDraft =
        ValidEntry("privacy.balanced.draft") with { Level = Level.Balanced, Status = TweakStatus.Draft };

    private static readonly TweakDefinition StrictVerified =
        ValidEntry("privacy.strict.verified") with { Level = Level.Strict, Status = TweakStatus.Verified };

    private static readonly TweakDefinition BalancedDeprecated =
        ValidEntry("privacy.balanced.deprecated") with { Level = Level.Balanced, Status = TweakStatus.Deprecated };

    private static readonly TweakDefinition PerformanceBasic =
        ValidEntry("performance.basic.verified") with { Category = Category.Performance, Level = Level.Basic, Status = TweakStatus.Verified };

    private static TweakCatalog Sample()
        => new([BasicVerified, BalancedDraft, StrictVerified, BalancedDeprecated, PerformanceBasic]);

    private static HashSet<string> Ids(IEnumerable<TweakDefinition> entries)
        => entries.Select(e => e.Id.Value).ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Count_and_entries_reflect_construction()
    {
        TweakCatalog catalog = Sample();

        Assert.Equal(5, catalog.Count);
        Assert.Equal(5, catalog.Entries.Count);
    }

    [Fact]
    public void TryGet_finds_a_known_id_and_rejects_an_unknown_one()
    {
        TweakCatalog catalog = Sample();

        Assert.True(catalog.TryGet(new TweakId("privacy.basic.verified"), out TweakDefinition found));
        Assert.Equal(BasicVerified.Id, found.Id);
        Assert.False(catalog.TryGet(new TweakId("nope.nope"), out _));
    }

    [Fact]
    public void Get_returns_the_entry_or_throws_for_an_unknown_id()
    {
        TweakCatalog catalog = Sample();

        Assert.Equal(BasicVerified.Id, catalog.Get(new TweakId("privacy.basic.verified")).Id);
        Assert.Throws<KeyNotFoundException>(() => catalog.Get(new TweakId("nope.nope")));
    }

    [Fact]
    public void InCategory_returns_only_that_category()
    {
        TweakCatalog catalog = Sample();

        TweakDefinition only = Assert.Single(catalog.InCategory(Category.Performance));
        Assert.Equal("performance.basic.verified", only.Id.Value);
    }

    [Fact]
    public void Select_off_returns_nothing()
    {
        Assert.Empty(Sample().Select(Category.Privacy, Level.Off, includeDraft: true));
    }

    [Fact]
    public void Select_includes_levels_up_to_the_dial_and_excludes_deprecated()
    {
        HashSet<string> selected = Ids(Sample().Select(Category.Privacy, Level.Strict, includeDraft: true));

        Assert.Equal(3, selected.Count);
        Assert.Contains("privacy.basic.verified", selected);
        Assert.Contains("privacy.balanced.draft", selected);
        Assert.Contains("privacy.strict.verified", selected);
        Assert.DoesNotContain("privacy.balanced.deprecated", selected);
    }

    [Fact]
    public void Select_excludes_higher_levels_than_the_dial()
    {
        HashSet<string> selected = Ids(Sample().Select(Category.Privacy, Level.Balanced, includeDraft: true));

        Assert.DoesNotContain("privacy.strict.verified", selected);
    }

    [Fact]
    public void Select_excludes_draft_entries_unless_requested()
    {
        HashSet<string> withoutDraft = Ids(Sample().Select(Category.Privacy, Level.Balanced, includeDraft: false));
        HashSet<string> withDraft = Ids(Sample().Select(Category.Privacy, Level.Balanced, includeDraft: true));

        Assert.DoesNotContain("privacy.balanced.draft", withoutDraft);
        Assert.Contains("privacy.basic.verified", withoutDraft);   // Verified is always included
        Assert.Contains("privacy.balanced.draft", withDraft);
    }

    [Fact]
    public void ApplicableTo_filters_by_applicability()
    {
        TweakDefinition armOnly = ValidEntry("privacy.arm.only")
            with
        { AppliesTo = new Applicability([], null, null, ["arm64"]) };
        TweakDefinition anywhere = ValidEntry("privacy.anywhere");
        var catalog = new TweakCatalog([armOnly, anywhere]);
        var x64 = new OperatingSystemFacts("Windows 11 Pro", "Professional", "24H2", 26100, 0, "x64", "Client");

        TweakDefinition only = Assert.Single(catalog.ApplicableTo(x64));
        Assert.Equal("privacy.anywhere", only.Id.Value);
    }
}
