namespace OpenTheWindows.Core.Reports;

/// <summary>Aggregate counts in a report.</summary>
internal sealed record ReportSummary(
    int Total,
    int Compliant,
    int Drift,
    int Managed,
    int NotApplicable,
    int Unknown,
    bool IsCompliant);
