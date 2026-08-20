namespace OpenTheWindows.Core.Reports;

/// <summary>One action's observed state in a report.</summary>
internal sealed record ReportAction(
    string Kind,
    string State,
    string Actual,
    string Desired,
    string Managed);
