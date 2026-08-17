using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Profiles;

/// <summary>Outcome of loading and validating one profile source.</summary>
/// <param name="Profile">The validated profile, or <see langword="null"/> when any error was found.</param>
/// <param name="Issues">All findings (errors and warnings) in discovery order.</param>
public sealed record ProfileLoadResult(Profile? Profile, IReadOnlyList<CatalogIssue> Issues)
{
    /// <summary><see langword="true"/> when no error-severity issue exists (warnings allowed).</summary>
    public bool IsValid => Profile is not null;

    /// <summary>Errors only.</summary>
    public IEnumerable<CatalogIssue> Errors => Issues.Where(i => i.Severity == CatalogIssueSeverity.Error);

    /// <summary>Warnings only.</summary>
    public IEnumerable<CatalogIssue> Warnings => Issues.Where(i => i.Severity == CatalogIssueSeverity.Warning);
}
