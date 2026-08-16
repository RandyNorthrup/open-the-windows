namespace OpenTheWindows.Core.Catalog;

/// <summary>Severity of a catalogue validation finding.</summary>
public enum CatalogIssueSeverity
{
    /// <summary>The catalogue cannot be used.</summary>
    Error = 0,

    /// <summary>Allowed but worth fixing (reported, does not fail validation).</summary>
    Warning = 1,
}
