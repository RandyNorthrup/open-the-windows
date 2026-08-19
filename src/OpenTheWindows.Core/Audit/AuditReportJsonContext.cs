using System.Text.Json.Serialization;

namespace OpenTheWindows.Core.Audit;

/// <summary>
/// Source-generated serialisation for the audit report (trim/AOT safe). Enums are
/// written with their framework labels (severity) and names (outcome); a null
/// signature is omitted so an unsigned report has no empty envelope.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(SignedAuditReport))]
[JsonSerializable(typeof(AuditReport))]
public sealed partial class AuditReportJsonContext : JsonSerializerContext;
