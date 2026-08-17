using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTheWindows.Core.Catalog;

/// <summary>
/// Source-generated JSON contract for catalogue files. Strict on purpose:
/// unknown members fail, enums are names, comments and trailing commas are
/// tolerated for hand-edited files.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    WriteIndented = true)]
[JsonSerializable(typeof(CatalogDocument))]
[JsonSerializable(typeof(TweakDefinition))]
[JsonSerializable(typeof(IReadOnlyList<TweakDefinition>))]
[JsonSerializable(typeof(IReadOnlyList<CatalogIssue>))]
[JsonSerializable(typeof(CatalogValidationReport))]
public sealed partial class CatalogJsonContext : JsonSerializerContext;
