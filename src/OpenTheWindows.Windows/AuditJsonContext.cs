using System.Text.Json.Serialization;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows;

/// <summary>Source-generated, single-line JSON contract for the JSONL audit trail.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    WriteIndented = false)]
[JsonSerializable(typeof(AuditEvent))]
internal sealed partial class AuditJsonContext : JsonSerializerContext;
