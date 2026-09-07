using System.Text.Json.Serialization;

namespace OpenTheWindows.Core.Releases;

/// <summary>Source-generated JSON for the GitHub Releases payload. Unknown members are skipped: that API is not a catalogue file.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip)]
[JsonSerializable(typeof(GitHubReleaseDto))]
internal sealed partial class GitHubReleaseJsonContext : JsonSerializerContext;
