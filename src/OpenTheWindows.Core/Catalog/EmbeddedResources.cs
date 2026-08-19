using System.Reflection;
using System.Text.Json;

namespace OpenTheWindows.Core.Catalog;

/// <summary>
/// Shared plumbing for the embedded-resource loaders (catalogue, profiles and
/// baselines): manifest-name normalisation, resource-text reading, and forgiving
/// JSON parsing that reports a failure as a <see cref="CatalogIssue"/> rather than
/// throwing. Centralised so the loaders do not each carry a copy of it.
/// </summary>
internal static class EmbeddedResources
{
    /// <summary>
    /// Maps the normalised logical name (forward slashes) to the actual manifest
    /// name. MSBuild's <c>%(RecursiveDir)</c> yields backslashes on Windows, so the
    /// embedded names contain a backslash before the file name; callers use
    /// forward-slash logical names such as "catalog/privacy/file.json".
    /// </summary>
    public static Dictionary<string, string> ManifestNames(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return assembly.GetManifestResourceNames().ToDictionary(n => n.Replace('\\', '/'), n => n, StringComparer.Ordinal);
    }

    /// <summary>Reads the text of an embedded resource by its forward-slash logical name.</summary>
    public static string Read(Assembly assembly, string logicalName)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        string actualName = ManifestNames(assembly).GetValueOrDefault(logicalName.Replace('\\', '/'), logicalName);
        using Stream stream = assembly.GetManifestResourceStream(actualName)
            ?? throw new InvalidOperationException($"Embedded resource '{logicalName}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Parses JSON, appending a <c>json</c> issue and returning null on failure instead of throwing.</summary>
    public static JsonDocument? Parse(string sourceName, string json, JsonDocumentOptions options, List<CatalogIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        try
        {
            return JsonDocument.Parse(json, options);
        }
        catch (JsonException ex)
        {
            issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, sourceName, ex.Path ?? string.Empty, "json", ex.Message));
            return null;
        }
    }
}
