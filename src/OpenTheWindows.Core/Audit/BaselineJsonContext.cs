using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTheWindows.Core.Audit;

/// <summary>
/// Source-generated JSON contract for baseline files. Strict on purpose: unknown
/// members fail, severity is written with its framework label (via
/// <see cref="JsonStringEnumMemberNameAttribute"/>), comments and trailing commas
/// are tolerated for hand-edited files.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    WriteIndented = true)]
[JsonSerializable(typeof(Baseline))]
[JsonSerializable(typeof(IReadOnlyList<Baseline>))]
[JsonSerializable(typeof(BaselineValidationReport))]
[JsonSerializable(typeof(IReadOnlyList<BaselineSummary>))]
public sealed partial class BaselineJsonContext : JsonSerializerContext;
