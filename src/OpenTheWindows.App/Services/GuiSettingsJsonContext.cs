using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTheWindows.App.Services;

/// <summary>Source-generated JSON for the GUI settings file.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(GuiSettingsSnapshot))]
internal sealed partial class GuiSettingsJsonContext : JsonSerializerContext;
