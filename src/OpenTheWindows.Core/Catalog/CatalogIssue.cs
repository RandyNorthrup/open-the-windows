namespace OpenTheWindows.Core.Catalog;

/// <summary>One validation finding.</summary>
/// <param name="Severity">Error or warning.</param>
/// <param name="Source">Catalogue file (logical name or path) the finding belongs to.</param>
/// <param name="Location">JSON pointer or tweak id locating the finding.</param>
/// <param name="Rule">Stable rule identifier, e.g. <c>schema</c>, <c>unique-id</c>, <c>sources-required</c>.</param>
/// <param name="Message">Human-readable explanation.</param>
public sealed record CatalogIssue(
    CatalogIssueSeverity Severity,
    string Source,
    string Location,
    string Rule,
    string Message)
{
    public override string ToString() => $"{Severity}: [{Rule}] {Source} {Location}: {Message}";
}
