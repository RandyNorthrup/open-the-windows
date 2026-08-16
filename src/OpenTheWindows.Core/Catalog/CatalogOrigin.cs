namespace OpenTheWindows.Core.Catalog;

/// <summary>Origin of a catalogue source; higher values override lower ones by id.</summary>
public enum CatalogOrigin
{
    /// <summary>Embedded in the product.</summary>
    BuiltIn = 0,

    /// <summary>Supplied by the operator (directory, profile bundle).</summary>
    Override = 1,
}
