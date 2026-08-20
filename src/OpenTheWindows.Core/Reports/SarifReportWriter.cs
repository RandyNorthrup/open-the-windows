using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Reports;

/// <summary>
/// Writes a scan report as SARIF 2.1.0. Every drifting entry becomes a result;
/// the drifting ids become the run's rules (one rule per id). A result's
/// <c>level</c> is derived from the entry's risk tier so a security tool can
/// triage by impact. Compliant, managed, unknown and not-applicable entries are
/// not findings and produce no result.
/// </summary>
public sealed class SarifReportWriter : IReportWriter
{
    /// <inheritdoc />
    public string Extension => "sarif";

    /// <inheritdoc />
    public void Write(ScanReport report, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);

        List<TweakObservation> drift = [.. report.Results.Where(r => r.State == ComplianceState.Drift)];
        List<string> ruleIds = [.. drift.Select(r => r.Id.Value).Distinct(StringComparer.Ordinal)];
        SarifEnvelope.Write(destination, writer =>
        {
            WriteTool(writer, ruleIds);
            WriteResults(writer, drift);
        });
    }

    private static void WriteTool(Utf8JsonWriter writer, IReadOnlyList<string> ruleIds)
    {
        writer.WriteStartObject("tool");
        writer.WriteStartObject("driver");
        writer.WriteString("name", AppInfo.Title);
        writer.WriteString("informationUri", AppInfo.HomepageUri);
        writer.WriteString("version", AppInfo.Version);

        writer.WriteStartArray("rules");
        foreach (string id in ruleIds)
        {
            writer.WriteStartObject();
            writer.WriteString("id", id);
            writer.WriteString("name", id);
            writer.WriteStartObject("shortDescription");
            writer.WriteString("text", string.Create(CultureInfo.InvariantCulture, $"Compliance of '{id}'."));
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteResults(Utf8JsonWriter writer, IReadOnlyList<TweakObservation> drift)
    {
        writer.WriteStartArray("results");
        foreach (TweakObservation observation in drift)
        {
            writer.WriteStartObject();
            writer.WriteString("ruleId", observation.Id.Value);
            writer.WriteString("level", RiskToLevel(observation.Risk));
            writer.WriteStartObject("message");
            writer.WriteString("text", string.Create(
                CultureInfo.InvariantCulture,
                $"'{observation.Id.Value}' is not compliant with the '{observation.Risk}' risk profile."));
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static string RiskToLevel(RiskTier risk) => risk switch
    {
        RiskTier.Breaking => "error",
        RiskTier.Advanced => "warning",
        _ => "note",
    };
}
