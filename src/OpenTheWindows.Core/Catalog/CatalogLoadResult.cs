namespace OpenTheWindows.Core.Catalog;

/// <summary>Outcome of loading one or more catalogue sources.</summary>
/// <param name="Catalog">The validated catalogue, or <see langword="null"/> when any error was found.</param>
/// <param name="Issues">All findings (errors and warnings) in source order.</param>
public sealed record CatalogLoadResult(TweakCatalog? Catalog, IReadOnlyList<CatalogIssue> Issues)
{
    /// <summary><see langword="true"/> when no error-severity issue exists (warnings allowed).</summary>
    public bool IsValid => Catalog is not null;

    /// <summary>Errors only.</summary>
    public IEnumerable<CatalogIssue> Errors => Issues.Where(i => i.Severity == CatalogIssueSeverity.Error);

    /// <summary>Warnings only.</summary>
    public IEnumerable<CatalogIssue> Warnings => Issues.Where(i => i.Severity == CatalogIssueSeverity.Warning);

    /// <summary>Returns the catalogue or throws with every issue attached.</summary>
    public TweakCatalog GetCatalogOrThrow()
        => Catalog ?? throw new CatalogValidationException(
            $"Catalogue validation failed with {Errors.Count()} error(s).", Issues);
}
