using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTheWindows.Core.Updates;

/// <summary>
/// Source-generated serialization for the end-of-servicing calendar. Unknown
/// members are rejected so a malformed data file fails loudly rather than being
/// silently misread.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(EndOfServicingDocument))]
internal sealed partial class EndOfServicingJsonContext : JsonSerializerContext;
