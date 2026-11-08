using System.Text.Json.Serialization;
using OpenTheWindows.Core.Persistence;

namespace OpenTheWindows.Windows.Security;

/// <summary>Source-generated JSON for the persistence baseline.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = true)]
[JsonSerializable(typeof(PersistenceSnapshot))]
internal sealed partial class PersistenceJsonContext : JsonSerializerContext;
