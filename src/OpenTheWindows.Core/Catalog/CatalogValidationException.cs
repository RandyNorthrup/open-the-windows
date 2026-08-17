namespace OpenTheWindows.Core.Catalog;

/// <summary>Thrown when a catalogue fails schema or structural validation.</summary>
public sealed class CatalogValidationException : Exception
{
    public CatalogValidationException()
        : this("The catalogue failed validation.", [])
    {
    }

    public CatalogValidationException(string message)
        : this(message, [])
    {
    }

    public CatalogValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Issues = [];
    }

    public CatalogValidationException(string message, IReadOnlyList<CatalogIssue> issues)
        : base(message)
    {
        ArgumentNullException.ThrowIfNull(issues);
        Issues = issues;
    }

    /// <summary>All findings; at least one has <see cref="CatalogIssueSeverity.Error"/> severity.</summary>
    public IReadOnlyList<CatalogIssue> Issues { get; }
}
