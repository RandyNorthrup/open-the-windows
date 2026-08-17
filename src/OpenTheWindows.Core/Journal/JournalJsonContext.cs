using System.Text.Json.Serialization;
using OpenTheWindows.Core.Catalog.Actions;

namespace OpenTheWindows.Core.Journal;

/// <summary>
/// Source-generated JSON contract for journal files (no reflection-based
/// serialisation; trim/AOT safe). Strict: unknown members fail on read, enums
/// are names, null union fields are omitted so each action carries only the
/// fields relevant to its kind.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(JournalRecord))]
[JsonSerializable(typeof(JournalSummary))]
[JsonSerializable(typeof(IReadOnlyList<JournalSummary>))]
[JsonSerializable(typeof(ITweakAction))]
public sealed partial class JournalJsonContext : JsonSerializerContext;
