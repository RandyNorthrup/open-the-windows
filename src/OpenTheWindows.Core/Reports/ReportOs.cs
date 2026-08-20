namespace OpenTheWindows.Core.Reports;

/// <summary>OS identity in a report.</summary>
internal sealed record ReportOs(
    string ProductName,
    string EditionId,
    string DisplayVersion,
    int Build,
    int Revision,
    string Architecture,
    string InstallationType,
    string FullBuild);
