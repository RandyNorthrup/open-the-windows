using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.Core.Reports;

/// <summary>Builds the serialisable <see cref="ReportDocument"/> from a <see cref="ScanReport"/>.</summary>
internal static class ReportProjection
{
    /// <summary>Version of the report document shape (independent of the product version).</summary>
    public const string SchemaVersion = "1.0";

    public static ReportDocument From(ScanReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var os = new ReportOs(
            report.Os.ProductName,
            report.Os.EditionId,
            report.Os.DisplayVersion,
            report.Os.Build,
            report.Os.Revision,
            report.Os.Architecture,
            report.Os.InstallationType,
            report.Os.FullBuild);

        var summary = new ReportSummary(
            report.Results.Count,
            report.CompliantCount,
            report.DriftCount,
            report.ManagedCount,
            report.Results.Count(r => r.State == ComplianceState.NotApplicable),
            report.Results.Count(r => r.State == ComplianceState.Unknown),
            report.IsCompliant);

        List<ReportEntry> entries = [.. report.Results.Select(ToEntry)];
        return new ReportDocument(SchemaVersion, report.At, report.ProfileName, os, summary, entries);
    }

    private static ReportEntry ToEntry(TweakObservation observation)
    {
        List<ReportAction> actions =
        [
            .. observation.Actions.Select(a => new ReportAction(
                a.Action.Kind,
                a.State.ToString(),
                a.Actual,
                a.Desired,
                a.Managed.ToString())),
        ];

        return new ReportEntry(
            observation.Id.Value,
            observation.State.ToString(),
            observation.Risk.ToString(),
            actions);
    }
}
