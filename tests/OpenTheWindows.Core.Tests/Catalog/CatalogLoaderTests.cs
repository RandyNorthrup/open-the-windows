using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;
using static OpenTheWindows.Core.Tests.Catalog.CatalogTestData;

namespace OpenTheWindows.Core.Tests.Catalog;

public sealed class CatalogLoaderTests
{
    private static CatalogLoadResult LoadOnly(string documentJson)
        => CatalogLoader.Load([new CatalogSource("test.json", documentJson, CatalogOrigin.BuiltIn)]);

    private static bool HasRule(CatalogLoadResult result, string rule)
        => result.Errors.Any(e => string.Equals(e.Rule, rule, StringComparison.Ordinal));

    private static CatalogLoadResult LoadWithFiles(params (string Name, string Json)[] files)
    {
        string dir = Path.Combine(Path.GetTempPath(), "otw-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            foreach ((string name, string json) in files)
            {
                File.WriteAllText(Path.Combine(dir, name), json);
            }

            return CatalogLoader.LoadWithDirectory(dir);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void A_single_valid_document_loads()
    {
        CatalogLoadResult result = LoadOnly(DocumentJson(EntryJson("test.valid.one")));

        Assert.True(result.IsValid);
        Assert.Equal(1, result.Catalog!.Count);
    }

    [Fact]
    public void Schema_violation_is_reported_with_a_json_pointer_location()
    {
        string badLevel = EntryJson("test.bad.level").Replace("\"level\": \"Basic\"", "\"level\": \"Nope\"", StringComparison.Ordinal);

        CatalogLoadResult result = LoadOnly(DocumentJson(badLevel));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            string.Equals(e.Rule, "schema", StringComparison.Ordinal)
            && e.Location.Contains("/entries/0", StringComparison.Ordinal));
    }

    [Fact]
    public void Unknown_action_kind_is_rejected()
    {
        string badKind = EntryJson("test.bad.kind").Replace("\"kind\": \"Registry\"", "\"kind\": \"Nonsense\"", StringComparison.Ordinal);

        CatalogLoadResult result = LoadOnly(DocumentJson(badKind));

        Assert.False(result.IsValid);
        Assert.True(HasRule(result, "schema"));
    }

    [Fact]
    public void Unknown_property_is_rejected()
    {
        string extra = EntryJson("test.extra.prop").Replace("\"tags\": []", "\"tags\": [], \"bogus\": 1", StringComparison.Ordinal);

        CatalogLoadResult result = LoadOnly(DocumentJson(extra));

        Assert.False(result.IsValid);
        Assert.True(HasRule(result, "schema"));
    }

    [Fact]
    public void Comments_and_trailing_commas_are_accepted()
    {
        string doc = $$"""
        {
          // a hand-authored comment
          "entries": [
            {{EntryJson("test.comment.ok")}},
          ]
        }
        """;

        CatalogLoadResult result = LoadOnly(doc);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(e => e.ToString())));
        Assert.Equal(1, result.Catalog!.Count);
    }

    [Fact]
    public void Malformed_json_reports_the_json_rule()
    {
        CatalogLoadResult result = LoadOnly("{ this is not json");

        Assert.False(result.IsValid);
        Assert.True(HasRule(result, "json"));
    }

    [Fact]
    public void An_override_directory_replaces_a_built_in_entry_by_id()
    {
        const string Id = "privacy.advertising-id.disable";
        CatalogLoadResult result = LoadWithFiles(("override.json", DocumentJson(EntryJson(Id, "OVERRIDDEN TITLE"))));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors.Select(e => e.ToString())));
        Assert.Equal("OVERRIDDEN TITLE", result.Catalog!.Get(new TweakId(Id)).Title);
    }

    [Fact]
    public void Duplicate_ids_within_the_directory_report_unique_id()
    {
        CatalogLoadResult result = LoadWithFiles(
            ("dupes.json", DocumentJson(EntryJson("test.duplicate.id", "First"), EntryJson("test.duplicate.id", "Second"))));

        Assert.False(result.IsValid);
        Assert.True(HasRule(result, "unique-id"));
    }

    [Fact]
    public void A_missing_directory_reports_directory_missing()
    {
        string missing = Path.Combine(Path.GetTempPath(), "otw-not-here-" + Guid.NewGuid().ToString("N"));

        CatalogLoadResult result = CatalogLoader.LoadWithDirectory(missing);

        Assert.False(result.IsValid);
        Assert.True(HasRule(result, "directory-missing"));
    }

    [Fact]
    public void GetCatalogOrThrow_throws_with_the_issues_attached()
    {
        CatalogLoadResult result = LoadOnly("{ not json");

        CatalogValidationException exception = Assert.Throws<CatalogValidationException>(result.GetCatalogOrThrow);

        Assert.NotEmpty(exception.Issues);
    }
}
