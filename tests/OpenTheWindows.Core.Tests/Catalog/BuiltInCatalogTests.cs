using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Tests.Catalog;

public sealed class BuiltInCatalogTests
{
    private static TweakCatalog Load()
    {
        CatalogLoadResult result = CatalogLoader.LoadBuiltIn();
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(e => e.ToString())));
        return result.Catalog!;
    }

    [Fact]
    public void Built_in_catalogue_loads_without_errors()
    {
        CatalogLoadResult result = CatalogLoader.LoadBuiltIn();

        Assert.Empty(result.Errors);
        Assert.NotNull(result.Catalog);
    }

    [Fact]
    public void Built_in_catalogue_has_at_least_twenty_entries()
    {
        Assert.True(Load().Count >= 20, "The built-in catalogue should ship at least 20 entries.");
    }

    [Fact]
    public void Every_category_is_represented()
    {
        TweakCatalog catalog = Load();

        foreach (Category category in Enum.GetValues<Category>())
        {
            Assert.NotEmpty(catalog.InCategory(category));
        }
    }

    [Fact]
    public void Entry_ids_are_unique()
    {
        TweakCatalog catalog = Load();

        int distinct = catalog.Entries.Select(e => e.Id).Distinct().Count();

        Assert.Equal(catalog.Count, distinct);
    }

    [Fact]
    public void Every_entry_has_at_least_one_https_source()
    {
        foreach (TweakDefinition entry in Load().Entries)
        {
            Assert.NotEmpty(entry.Sources);
            Assert.All(entry.Sources, source => Assert.Equal(Uri.UriSchemeHttps, source.Scheme));
        }
    }

    [Fact]
    public void No_verified_entry_lacks_evidence()
    {
        IEnumerable<TweakDefinition> verified = Load().Entries.Where(e => e.Status == TweakStatus.Verified);

        foreach (TweakDefinition entry in verified)
        {
            Assert.NotEmpty(entry.VerifiedOn);
            Assert.All(entry.VerifiedOn, v => Assert.False(string.IsNullOrWhiteSpace(v.Evidence)));
        }
    }
}
