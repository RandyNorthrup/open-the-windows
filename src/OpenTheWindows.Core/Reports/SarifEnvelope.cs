using System.Text.Json;

namespace OpenTheWindows.Core.Reports;

/// <summary>
/// Writes the SARIF 2.1.0 document envelope (schema, version, and a single run
/// object) around a caller-supplied run body, shared by the report writers.
/// </summary>
internal static class SarifEnvelope
{
    private const string Schema = "https://json.schemastore.org/sarif-2.1.0.json";
    private const string Version = "2.1.0";

    /// <summary>
    /// Writes a SARIF document with one run, invoking <paramref name="writeRun"/> to
    /// emit the run's <c>tool</c> and <c>results</c>. Does not close the stream.
    /// </summary>
    public static void Write(Stream destination, Action<Utf8JsonWriter> writeRun)
    {
        using var writer = new Utf8JsonWriter(destination, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteString("$schema", Schema);
        writer.WriteString("version", Version);

        writer.WriteStartArray("runs");
        writer.WriteStartObject();

        writeRun(writer);

        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
