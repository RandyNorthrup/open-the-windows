using System.Text;
using OpenTheWindows.Core.Reports;

namespace OpenTheWindows.Core.Audit;

/// <summary>
/// Writes an audit report as RFC 4180 CSV, one row per rule. Uses <c>\n</c> line
/// endings for deterministic output and never closes the caller's stream.
/// </summary>
public sealed class AuditCsvWriter : IAuditReportWriter
{
    private const string Header = "ruleId,severity,outcome,title,tweakIds,evidence";

    /// <inheritdoc />
    public string Extension => "csv";

    /// <inheritdoc />
    public void Write(AuditReport report, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);

        using var writer = new StreamWriter(destination, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
        {
            NewLine = "\n",
        };

        writer.WriteLine(Header);
        foreach (AuditRuleResult result in report.Results)
        {
            CsvWriting.WriteRow(
                writer,
                result.RuleId,
                BaselineSeverityText.Label(result.Severity),
                result.Outcome.ToString(),
                result.Title,
                string.Join(' ', result.TweakIds.Select(t => t.Value)),
                result.Evidence);
        }
    }
}
