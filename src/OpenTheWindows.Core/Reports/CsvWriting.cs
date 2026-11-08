namespace OpenTheWindows.Core.Reports;

/// <summary>RFC 4180 CSV field escaping and row writing, shared by the report writers.</summary>
internal static class CsvWriting
{
    /// <summary>
    /// Characters that make a spreadsheet treat a cell as a formula. A report field can
    /// carry text a standard user controls (a value read from their own registry hive),
    /// so such a field is prefixed with an apostrophe and opened as plain text.
    /// </summary>
    private static readonly char[] FormulaTriggers = ['=', '+', '-', '@', '\t', '\r'];

    /// <summary>Writes one CSV row (fields joined by commas, each escaped), with the writer's newline.</summary>
    public static void WriteRow(TextWriter writer, params string[] fields)
        => writer.WriteLine(string.Join(',', fields.Select(Escape)));

    /// <summary>
    /// Neutralises a leading formula character, then quotes the field when it contains
    /// a comma, quote or newline, doubling embedded quotes.
    /// </summary>
    public static string Escape(string field)
    {
        string safe = field.Length > 0 && Array.IndexOf(FormulaTriggers, field[0]) >= 0 ? "'" + field : field;
        bool mustQuote = safe.Contains(',', StringComparison.Ordinal)
            || safe.Contains('"', StringComparison.Ordinal)
            || safe.Contains('\n', StringComparison.Ordinal)
            || safe.Contains('\r', StringComparison.Ordinal);
        return mustQuote
            ? string.Concat("\"", safe.Replace("\"", "\"\"", StringComparison.Ordinal), "\"")
            : safe;
    }
}
