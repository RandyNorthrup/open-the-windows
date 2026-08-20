using System.Text;
using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.Core.Reports;

/// <summary>
/// Writes a scan report as RFC 4180 CSV: one row per action, with a single row
/// carrying empty action columns for entries that have no actions (for example
/// a not-applicable entry). Uses <c>\n</c> line endings for deterministic output.
/// </summary>
public sealed class CsvReportWriter : IReportWriter
{
    private const string Header = "id,state,risk,kind,actionState,actual,desired,managed";

    /// <inheritdoc />
    public string Extension => "csv";

    /// <inheritdoc />
    public void Write(ScanReport report, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);

        ReportDocument document = ReportProjection.From(report);
        using var writer = new StreamWriter(destination, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
        {
            NewLine = "\n",
        };

        writer.WriteLine(Header);
        foreach (ReportEntry entry in document.Results)
        {
            if (entry.Actions.Count == 0)
            {
                CsvWriting.WriteRow(writer, entry.Id, entry.State, entry.Risk, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
                continue;
            }

            foreach (ReportAction action in entry.Actions)
            {
                CsvWriting.WriteRow(writer, entry.Id, entry.State, entry.Risk, action.Kind, action.State, action.Actual, action.Desired, action.Managed);
            }
        }
    }
}
