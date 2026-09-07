using System.Text.Json.Serialization;

namespace OpenTheWindows.Core.Releases;

/// <summary>The GitHub Releases API fields this product reads. Extra members are ignored.</summary>
internal sealed record GitHubReleaseDto(
    [property: JsonPropertyName("tag_name")] string? TagName,
    [property: JsonPropertyName("html_url")] string? HtmlUrl,
    [property: JsonPropertyName("draft")] bool Draft,
    [property: JsonPropertyName("prerelease")] bool Prerelease);
