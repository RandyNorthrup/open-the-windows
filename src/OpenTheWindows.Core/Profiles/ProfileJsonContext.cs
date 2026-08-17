using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTheWindows.Core.Profiles;

/// <summary>
/// Source-generated JSON contract for profile files: camelCase members, string
/// enums (member names), comments and trailing commas tolerated for hand-edited
/// files. Unknown members are <em>skipped</em> here (not rejected) because the
/// profile JSON Schema — which every profile passes before deserialization — is
/// the strict authority (<c>additionalProperties:false</c>) and it explicitly
/// permits the editor <c>$schema</c> hint that the domain type does not model.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    WriteIndented = true)]
[JsonSerializable(typeof(Profile))]
[JsonSerializable(typeof(IReadOnlyList<Profile>))]
public sealed partial class ProfileJsonContext : JsonSerializerContext;
