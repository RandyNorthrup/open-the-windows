using System.Globalization;
using System.Net;
using System.Text;
using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.Core.Reports;

/// <summary>
/// Writes a scan report as a single self-contained HTML file: inline CSS, no
/// external stylesheets, scripts, fonts or images, so it renders offline and
/// can be archived as evidence. Deterministic (<c>\n</c> line endings).
/// </summary>
public sealed class HtmlReportWriter : IReportWriter
{
    private const string Style =
        "body{font-family:Segoe UI,Arial,sans-serif;margin:2rem;color:#1b1b1b}" +
        "h1{font-size:1.4rem}table{border-collapse:collapse;margin:1rem 0;width:100%}" +
        "th,td{border:1px solid #ccc;padding:.35rem .6rem;text-align:left;vertical-align:top}" +
        "th{background:#f3f3f3}.drift{color:#a80000;font-weight:600}.compliant{color:#107c10}" +
        ".managed{color:#8a6d00}.muted{color:#666}";

    /// <inheritdoc />
    public string Extension => "html";

    /// <inheritdoc />
    public void Write(ScanReport report, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);

        ReportDocument document = ReportProjection.From(report);
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n")
            .Append("<meta charset=\"utf-8\">\n")
            .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n")
            .Append("<title>").Append(Encode(AppInfo.Title)).Append(" — Scan report</title>\n")
            .Append("<style>").Append(Style).Append("</style>\n</head>\n<body>\n");

        html.Append("<h1>").Append(Encode(AppInfo.Title)).Append(" — Scan report</h1>\n");
        html.Append("<p class=\"muted\">Profile <strong>").Append(Encode(document.Profile))
            .Append("</strong> · generated ")
            .Append(Encode(document.GeneratedAt.ToString("u", CultureInfo.InvariantCulture)))
            .Append("</p>\n");
        html.Append("<p class=\"muted\">")
            .Append(Encode(document.Os.ProductName)).Append(" (").Append(Encode(document.Os.EditionId))
            .Append(") build ").Append(Encode(document.Os.FullBuild)).Append(' ')
            .Append(Encode(document.Os.Architecture)).Append("</p>\n");

        AppendSummary(html, document.Summary);
        AppendResults(html, document.Results);

        html.Append("</body>\n</html>\n");

        // Do not dispose/close the caller's stream.
        byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(html.ToString());
        destination.Write(bytes, 0, bytes.Length);
    }

    private static void AppendSummary(StringBuilder html, ReportSummary summary)
    {
        html.Append("<table>\n<thead><tr>")
            .Append("<th>Total</th><th>Compliant</th><th>Drift</th><th>Managed</th>")
            .Append("<th>Not applicable</th><th>Unknown</th><th>Compliant?</th>")
            .Append("</tr></thead>\n<tbody><tr>")
            .Append("<td>").Append(summary.Total).Append("</td>")
            .Append("<td>").Append(summary.Compliant).Append("</td>")
            .Append("<td class=\"drift\">").Append(summary.Drift).Append("</td>")
            .Append("<td>").Append(summary.Managed).Append("</td>")
            .Append("<td>").Append(summary.NotApplicable).Append("</td>")
            .Append("<td>").Append(summary.Unknown).Append("</td>")
            .Append("<td>").Append(summary.IsCompliant ? "yes" : "no").Append("</td>")
            .Append("</tr></tbody>\n</table>\n");
    }

    private static void AppendResults(StringBuilder html, IReadOnlyList<ReportEntry> results)
    {
        html.Append("<table>\n<thead><tr><th>Id</th><th>State</th><th>Risk</th><th>Actions</th></tr></thead>\n<tbody>\n");
        foreach (ReportEntry entry in results)
        {
            html.Append("<tr><td>").Append(Encode(entry.Id)).Append("</td>")
                .Append("<td class=\"").Append(StateClass(entry.State)).Append("\">").Append(Encode(entry.State)).Append("</td>")
                .Append("<td>").Append(Encode(entry.Risk)).Append("</td><td>");
            AppendActions(html, entry.Actions);
            html.Append("</td></tr>\n");
        }

        html.Append("</tbody>\n</table>\n");
    }

    private static void AppendActions(StringBuilder html, IReadOnlyList<ReportAction> actions)
    {
        if (actions.Count == 0)
        {
            html.Append("<span class=\"muted\">—</span>");
            return;
        }

        for (int i = 0; i < actions.Count; i++)
        {
            ReportAction action = actions[i];
            if (i > 0)
            {
                html.Append("<br>\n");
            }

            html.Append(Encode(action.Kind)).Append(": ")
                .Append(Encode(action.State)).Append(" (actual ").Append(Encode(action.Actual))
                .Append(", desired ").Append(Encode(action.Desired)).Append(')');
        }
    }

    private static string StateClass(string state) => state switch
    {
        "Drift" => "drift",
        "Compliant" => "compliant",
        "Managed" => "managed",
        _ => "muted",
    };

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
