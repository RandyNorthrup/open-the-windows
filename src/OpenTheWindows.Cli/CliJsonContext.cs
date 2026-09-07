using System.Text.Json.Serialization;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Releases;
using OpenTheWindows.Core.Updates;

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
[JsonSerializable(typeof(IReadOnlyList<DiscoveredUpdate>))]
[JsonSerializable(typeof(AppReleaseCheckResult))]
internal sealed partial class CliJsonContext : JsonSerializerContext;
