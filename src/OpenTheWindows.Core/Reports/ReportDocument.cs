namespace OpenTheWindows.Core.Reports;

/// <summary>Serialisable projection of a scan report (the JSON/HTML shape).</summary>
internal sealed record ReportDocument(
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    string Profile,
    ReportOs Os,
    ReportSummary Summary,
    IReadOnlyList<ReportEntry> Results);
