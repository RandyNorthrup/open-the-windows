namespace OpenTheWindows.Core.Reports;

/// <summary>RFC 4180 CSV field escaping and row writing, shared by the report writers.</summary>
internal static class CsvWriting
{
    /// <summary>Writes one CSV row (fields joined by commas, each escaped), with the writer's newline.</summary>
    public static void WriteRow(TextWriter writer, params string[] fields)
        => writer.WriteLine(string.Join(',', fields.Select(Escape)));

    /// <summary>Quotes a field when it contains a comma, quote or newline, doubling embedded quotes.</summary>
    public static string Escape(string field)
    {
        bool mustQuote = field.Contains(',', StringComparison.Ordinal)
            || field.Contains('"', StringComparison.Ordinal)
            || field.Contains('\n', StringComparison.Ordinal)
            || field.Contains('\r', StringComparison.Ordinal);
        return mustQuote
            ? string.Concat("\"", field.Replace("\"", "\"\"", StringComparison.Ordinal), "\"")
            : field;
    }
}
