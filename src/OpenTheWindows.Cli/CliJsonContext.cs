using System.Text.Json.Serialization;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.Cli;

/// <summary>
/// Source-generated JSON contract for CLI output. Source generation keeps the
/// CLI trimmable/AOT-friendly and makes the output schema explicit: every type
/// emitted on stdout is listed here.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(SystemReport))]
[JsonSerializable(typeof(IReadOnlyList<HealthCheckResult>))]
[JsonSerializable(typeof(ApplyJsonReport))]
[JsonSerializable(typeof(IReadOnlyList<JournalSummary>))]
[JsonSerializable(typeof(IReadOnlyList<UserProfile>))]
internal sealed partial class CliJsonContext : JsonSerializerContext;
