using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Audit;

/// <summary>Outcome of loading a set of baseline documents (the built-ins, optionally plus a directory).</summary>
/// <param name="Baselines">The baselines that parsed and schema-validated, in id order.</param>
/// <param name="Issues">All findings across every source, in source order.</param>
public sealed record BaselineSetLoadResult(IReadOnlyList<Baseline> Baselines, IReadOnlyList<CatalogIssue> Issues)
{
    /// <summary><see langword="true"/> when no error-severity issue exists (warnings allowed).</summary>
    public bool IsValid => !Issues.Any(i => i.Severity == CatalogIssueSeverity.Error);

    /// <summary>Errors only.</summary>
    public IEnumerable<CatalogIssue> Errors => Issues.Where(i => i.Severity == CatalogIssueSeverity.Error);

    /// <summary>Warnings only.</summary>
    public IEnumerable<CatalogIssue> Warnings => Issues.Where(i => i.Severity == CatalogIssueSeverity.Warning);
}
