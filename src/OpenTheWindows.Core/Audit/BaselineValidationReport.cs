using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Audit;

/// <summary>Machine-readable outcome of <c>otw audit validate</c>.</summary>
/// <param name="IsValid">No error-severity issues.</param>
/// <param name="BaselineCount">Baselines loaded.</param>
/// <param name="RuleCount">Total rules across all loaded baselines.</param>
/// <param name="Issues">All findings.</param>
public sealed record BaselineValidationReport(
    bool IsValid,
    int BaselineCount,
    int RuleCount,
    IReadOnlyList<CatalogIssue> Issues);
