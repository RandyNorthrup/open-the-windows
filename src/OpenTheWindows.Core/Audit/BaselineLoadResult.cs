using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Audit;

/// <summary>Outcome of loading one baseline document.</summary>
/// <param name="Baseline">The parsed baseline, or <see langword="null"/> when any error was found.</param>
/// <param name="Issues">All findings (errors and warnings) in source order.</param>
public sealed record BaselineLoadResult(Baseline? Baseline, IReadOnlyList<CatalogIssue> Issues)
{
    /// <summary><see langword="true"/> when the baseline parsed and schema-validated.</summary>
    public bool IsValid => Baseline is not null;

    /// <summary>Errors only.</summary>
    public IEnumerable<CatalogIssue> Errors => Issues.Where(i => i.Severity == CatalogIssueSeverity.Error);
}
