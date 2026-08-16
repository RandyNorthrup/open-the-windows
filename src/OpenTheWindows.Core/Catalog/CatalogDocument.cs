using System.Text.Json.Serialization;

namespace OpenTheWindows.Core.Catalog;

/// <summary>Root of one catalogue JSON file.</summary>
/// <param name="Schema">Optional <c>$schema</c> reference for editors.</param>
/// <param name="Entries">Entries in this file.</param>
public sealed record CatalogDocument(
    [property: JsonPropertyName("$schema")] string? Schema,
    IReadOnlyList<TweakDefinition> Entries);
