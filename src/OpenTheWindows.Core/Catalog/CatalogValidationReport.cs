namespace OpenTheWindows.Core.Catalog;

/// <summary>Machine-readable outcome of <c>otw catalog validate</c>.</summary>
/// <param name="IsValid">No error-severity issues.</param>
/// <param name="EntryCount">Entries loaded when valid; 0 otherwise.</param>
/// <param name="Issues">All findings.</param>
public sealed record CatalogValidationReport(bool IsValid, int EntryCount, IReadOnlyList<CatalogIssue> Issues)
{
    /// <summary>Builds the report from a load result.</summary>
    public static CatalogValidationReport From(CatalogLoadResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new CatalogValidationReport(result.IsValid, result.Catalog?.Count ?? 0, result.Issues);
    }
}
