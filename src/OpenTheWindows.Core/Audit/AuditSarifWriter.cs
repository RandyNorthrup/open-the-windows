using System.Text.Json;
using OpenTheWindows.Core.Reports;

namespace OpenTheWindows.Core.Audit;

/// <summary>
/// Writes an audit report as SARIF 2.1.0. The run's rules are the baseline rules
/// (id = the framework control id, with severity and outcome as properties); every
/// failing rule becomes a result whose <c>level</c> is derived from its severity so
/// a security tool can triage by impact. Passing, manual, not-applicable and
/// unknown rules produce no result.
/// </summary>
public sealed class AuditSarifWriter : IAuditReportWriter
{
    /// <inheritdoc />
    public string Extension => "sarif";

    /// <inheritdoc />
    public void Write(AuditReport report, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);

        List<AuditRuleResult> failures = [.. report.Results.Where(r => r.Outcome == AuditOutcome.Fail)];
        SarifEnvelope.Write(destination, writer =>
        {
            WriteTool(writer, report);
            WriteResults(writer, failures);
        });
    }

    private static void WriteTool(Utf8JsonWriter writer, AuditReport report)
    {
        writer.WriteStartObject("tool");
        writer.WriteStartObject("driver");
        writer.WriteString("name", AppInfo.Title);
        writer.WriteString("informationUri", AppInfo.HomepageUri);
        writer.WriteString("version", AppInfo.Version);

        writer.WriteStartArray("rules");
        foreach (AuditRuleResult rule in report.Results)
        {
            writer.WriteStartObject();
            writer.WriteString("id", rule.RuleId);
            writer.WriteString("name", rule.RuleId);
            writer.WriteStartObject("shortDescription");
            writer.WriteString("text", rule.Title);
            writer.WriteEndObject();
            writer.WriteStartObject("properties");
            writer.WriteString("severity", BaselineSeverityText.Label(rule.Severity));
            writer.WriteString("outcome", rule.Outcome.ToString());
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteResults(Utf8JsonWriter writer, IReadOnlyList<AuditRuleResult> failures)
    {
        writer.WriteStartArray("results");
        foreach (AuditRuleResult rule in failures)
        {
            writer.WriteStartObject();
            writer.WriteString("ruleId", rule.RuleId);
            writer.WriteString("level", SeverityToLevel(rule.Severity));
            writer.WriteStartObject("message");
            writer.WriteString("text", rule.Evidence);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static string SeverityToLevel(BaselineSeverity severity) => severity switch
    {
        BaselineSeverity.CatI => "error",
        BaselineSeverity.CatII => "warning",
        BaselineSeverity.CatIII => "note",
        BaselineSeverity.L1 => "warning",
        BaselineSeverity.L2 => "note",
        BaselineSeverity.Bl => "warning",
        BaselineSeverity.Ng => "note",
        BaselineSeverity.Baseline => "warning",
        _ => "note",
    };
}
