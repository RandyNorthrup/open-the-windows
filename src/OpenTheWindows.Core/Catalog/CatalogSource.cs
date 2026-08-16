namespace OpenTheWindows.Core.Catalog;

/// <summary>One catalogue JSON document to load.</summary>
/// <param name="Name">Display name (logical resource name or file path).</param>
/// <param name="Json">Document text.</param>
/// <param name="Origin">Built-in or operator override.</param>
public sealed record CatalogSource(string Name, string Json, CatalogOrigin Origin);
