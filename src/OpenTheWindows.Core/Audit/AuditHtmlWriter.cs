using System.Globalization;
using System.Net;
using System.Text;

namespace OpenTheWindows.Core.Audit;

/// <summary>
/// Writes an audit report as a single self-contained HTML file: inline CSS, no
/// external stylesheets, scripts, fonts or images, so it renders offline and can
/// be archived as evidence. An executive summary (score and outcome counts) is
/// followed by one table per severity present. Deterministic (<c>\n</c> endings).
/// </summary>
public sealed class AuditHtmlWriter : IAuditReportWriter
{
    private const string Style =
        "body{font-family:Segoe UI,Arial,sans-serif;margin:2rem;color:#1b1b1b}" +
        "h1{font-size:1.4rem}h2{font-size:1.1rem;margin-top:1.6rem}" +
        "table{border-collapse:collapse;margin:1rem 0;width:100%}" +
        "th,td{border:1px solid #ccc;padding:.35rem .6rem;text-align:left;vertical-align:top}" +
        "th{background:#f3f3f3}.fail{color:#a80000;font-weight:600}.pass{color:#107c10}" +
        ".manual{color:#8a6d00}.muted{color:#666}.score{font-size:2rem;font-weight:700}";

    /// <inheritdoc />
    public string Extension => "html";

    /// <inheritdoc />
    public void Write(AuditReport report, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n")
            .Append("<meta charset=\"utf-8\">\n")
            .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n")
            .Append("<title>").Append(Encode(AppInfo.Title)).Append(" — Audit report</title>\n")
            .Append("<style>").Append(Style).Append("</style>\n</head>\n<body>\n");

        html.Append("<h1>").Append(Encode(AppInfo.Title)).Append(" — Audit report</h1>\n");
        html.Append("<p class=\"muted\">Baseline <strong>").Append(Encode(report.BaselineTitle))
            .Append("</strong> (").Append(Encode(report.BaselineId)).Append(") · generated ")
            .Append(Encode(report.At.ToString("u", CultureInfo.InvariantCulture)))
            .Append("</p>\n");
        html.Append("<p class=\"muted\">")
            .Append(Encode(report.Os.ProductName)).Append(" (").Append(Encode(report.Os.EditionId))
            .Append(") build ").Append(Encode(report.Os.FullBuild)).Append(' ')
            .Append(Encode(report.Os.Architecture)).Append("</p>\n");

        AppendSummary(html, report);
        AppendResults(html, report.Results);

        html.Append("</body>\n</html>\n");

        // Do not dispose/close the caller's stream.
        byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(html.ToString());
        destination.Write(bytes, 0, bytes.Length);
    }

    private static void AppendSummary(StringBuilder html, AuditReport report)
    {
        AuditSummary summary = report.Summary;
        html.Append("<p class=\"score\">Score: ").Append(report.Score).Append(" / 100</p>\n");
        html.Append("<table>\n<thead><tr>")
            .Append("<th>Total</th><th>Pass</th><th>Fail</th><th>Not applicable</th><th>Manual</th><th>Unknown</th>")
            .Append("</tr></thead>\n<tbody><tr>")
            .Append("<td>").Append(summary.Total).Append("</td>")
            .Append("<td class=\"pass\">").Append(summary.Pass).Append("</td>")
            .Append("<td class=\"fail\">").Append(summary.Fail).Append("</td>")
            .Append("<td>").Append(summary.NotApplicable).Append("</td>")
            .Append("<td>").Append(summary.Manual).Append("</td>")
            .Append("<td>").Append(summary.Unknown).Append("</td>")
            .Append("</tr></tbody>\n</table>\n");
    }

    private static void AppendResults(StringBuilder html, IReadOnlyList<AuditRuleResult> results)
    {
        foreach (BaselineSeverity severity in Enum.GetValues<BaselineSeverity>())
        {
            List<AuditRuleResult> rules = [.. results.Where(r => r.Severity == severity)];
            if (rules.Count == 0)
            {
                continue;
            }

            html.Append("<h2>").Append(Encode(BaselineSeverityText.Label(severity))).Append("</h2>\n");
            html.Append("<table>\n<thead><tr><th>Rule</th><th>Outcome</th><th>Title</th><th>Evidence</th></tr></thead>\n<tbody>\n");
            foreach (AuditRuleResult rule in rules)
            {
                html.Append("<tr><td>").Append(Encode(rule.RuleId)).Append("</td>")
                    .Append("<td class=\"").Append(OutcomeClass(rule.Outcome)).Append("\">").Append(Encode(rule.Outcome.ToString())).Append("</td>")
                    .Append("<td>").Append(Encode(rule.Title)).Append("</td>")
                    .Append("<td>").Append(Encode(rule.Evidence)).Append("</td></tr>\n");
            }

            html.Append("</tbody>\n</table>\n");
        }
    }

    private static string OutcomeClass(AuditOutcome outcome) => outcome switch
    {
        AuditOutcome.Fail => "fail",
        AuditOutcome.Pass => "pass",
        AuditOutcome.Manual => "manual",
        _ => "muted",
    };

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
