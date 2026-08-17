using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;
using static OpenTheWindows.Core.Tests.Catalog.CatalogTestData;

namespace OpenTheWindows.Core.Tests.Engine;

public sealed class NamedProfileTests
{
    [Theory]
    [InlineData("basic", Level.Basic)]
    [InlineData("BALANCED", Level.Balanced)]
    [InlineData("strict", Level.Strict)]
    [InlineData("paranoid", Level.Paranoid)]
    public void TryResolve_maps_known_names_case_insensitively(string name, Level expected)
    {
        Assert.True(NamedProfile.TryResolve(name, out Level level));
        Assert.Equal(expected, level);
    }

    [Fact]
    public void TryResolve_rejects_an_unknown_name()
    {
        Assert.False(NamedProfile.TryResolve("nope", out _));
    }

    [Fact]
    public void Select_applies_level_status_off_and_deprecated_filters()
    {
        var catalog = new TweakCatalog(
        [
            ValidEntry("cat.a.off") with { Level = Level.Off, Status = TweakStatus.Verified },
            ValidEntry("cat.a.basic-verified") with { Level = Level.Basic, Status = TweakStatus.Verified },
            ValidEntry("cat.a.basic-draft") with { Level = Level.Basic, Status = TweakStatus.Draft },
            ValidEntry("cat.a.basic-deprecated") with { Level = Level.Basic, Status = TweakStatus.Deprecated },
            ValidEntry("cat.a.strict-verified") with { Level = Level.Strict, Status = TweakStatus.Verified },
        ]);

        string[] verifiedAtBasic = [.. NamedProfile.Select(catalog, Level.Basic, includeDraft: false).Select(e => e.Id.Value)];
        Assert.Equal(["cat.a.basic-verified"], verifiedAtBasic);

        var withDraftIds = NamedProfile.Select(catalog, Level.Basic, includeDraft: true)
            .Select(e => e.Id.Value)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("cat.a.basic-verified", withDraftIds);
        Assert.Contains("cat.a.basic-draft", withDraftIds);
        Assert.DoesNotContain("cat.a.basic-deprecated", withDraftIds);
        Assert.DoesNotContain("cat.a.off", withDraftIds);
        Assert.DoesNotContain("cat.a.strict-verified", withDraftIds);

        var atStrict = NamedProfile.Select(catalog, Level.Strict, includeDraft: false)
            .Select(e => e.Id.Value)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("cat.a.strict-verified", atStrict);
    }
}
