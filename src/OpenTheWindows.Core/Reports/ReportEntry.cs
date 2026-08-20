namespace OpenTheWindows.Core.Reports;

/// <summary>One catalogue entry's observed state in a report.</summary>
internal sealed record ReportEntry(
    string Id,
    string State,
    string Risk,
    IReadOnlyList<ReportAction> Actions);
