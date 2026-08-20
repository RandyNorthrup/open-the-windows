using System.Text.Json.Serialization;

namespace OpenTheWindows.Core.Reports;

/// <summary>
/// Source-generated serialisation for the JSON report document, so the report
/// writers use no reflection-based serialisation (trim/AOT safe).
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = false)]
[JsonSerializable(typeof(ReportDocument))]
internal sealed partial class ReportJsonContext : JsonSerializerContext;
