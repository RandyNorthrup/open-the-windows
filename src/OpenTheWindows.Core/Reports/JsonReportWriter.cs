using System.Text.Json;
using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.Core.Reports;

/// <summary>Writes a scan report as indented JSON (source-generated, camelCase).</summary>
public sealed class JsonReportWriter : IReportWriter
{
    /// <inheritdoc />
    public string Extension => "json";

    /// <inheritdoc />
    public void Write(ScanReport report, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);

        ReportDocument document = ReportProjection.From(report);
        using var writer = new Utf8JsonWriter(destination, new JsonWriterOptions { Indented = true });
        JsonSerializer.Serialize(writer, document, ReportJsonContext.Default.ReportDocument);
    }
}
