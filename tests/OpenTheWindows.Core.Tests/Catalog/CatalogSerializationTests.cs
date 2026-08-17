using System.Text.Json;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;
using static OpenTheWindows.Core.Tests.Catalog.CatalogTestData;

namespace OpenTheWindows.Core.Tests.Catalog;

public sealed class CatalogSerializationTests
{
    [Fact]
    public void Tweak_definition_round_trips_through_the_source_generated_context()
    {
        TweakDefinition original = ValidEntry("privacy.round.trip");

        string json = JsonSerializer.Serialize(original, CatalogJsonContext.Default.TweakDefinition);
        TweakDefinition? restored = JsonSerializer.Deserialize(json, CatalogJsonContext.Default.TweakDefinition);

        Assert.NotNull(restored);
        Assert.Equal(original.Id, restored.Id);
        Assert.Equal(original.Title, restored.Title);
        Assert.Equal(original.Category, restored.Category);
        Assert.Equal(original.Actions.Count, restored.Actions.Count);
        Assert.Equal(original.Actions[0].Kind, restored.Actions[0].Kind);

        // Re-serialising the restored entry yields identical JSON: the shape is stable.
        Assert.Equal(json, JsonSerializer.Serialize(restored, CatalogJsonContext.Default.TweakDefinition));
    }

    [Fact]
    public void Property_names_are_camel_case_and_enums_are_names()
    {
        string json = JsonSerializer.Serialize(ValidEntry(), CatalogJsonContext.Default.TweakDefinition);

        Assert.Contains("\"appliesTo\"", json, StringComparison.Ordinal);
        Assert.Contains("\"category\": \"Privacy\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Category\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Tweak_id_converter_reads_a_valid_id()
    {
        TweakId id = JsonSerializer.Deserialize<TweakId>("\"privacy.advertising-id.disable\"");

        Assert.Equal("privacy.advertising-id.disable", id.Value);
    }

    [Fact]
    public void Tweak_id_converter_rejects_an_invalid_id()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TweakId>("\"Not A Valid Id\""));
    }

    [Fact]
    public void Unknown_members_are_rejected_by_the_context()
    {
        // Defence in depth: even if a document slipped past the schema, the
        // source-generated context disallows unmapped members.
        const string Json = """{ "entries": [], "unexpected": 1 }""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(Json, CatalogJsonContext.Default.CatalogDocument));
    }
}
