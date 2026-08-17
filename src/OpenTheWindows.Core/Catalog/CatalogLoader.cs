using System.Reflection;
using System.Text.Json;
using Json.Schema;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Catalog;

/// <summary>
/// Loads catalogue documents (embedded built-in files, operator directories),
/// validates each against the JSON Schema and the structural rules, and
/// produces a <see cref="TweakCatalog"/>. Never throws for catalogue content
/// problems; they are reported as <see cref="CatalogIssue"/>s.
/// </summary>
public static class CatalogLoader
{
    /// <summary>Logical name prefix of embedded catalogue files.</summary>
    public const string EmbeddedPrefix = "catalog/";

    /// <summary>Logical name of the embedded JSON Schema.</summary>
    public const string SchemaResourceName = "catalog/schema/tweak.schema.json";

    private const string SchemaFolder = "catalog/schema/";
    private const string JsonExtension = ".json";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly Lazy<JsonSchema> Schema = new(LoadSchema);

    /// <summary>The embedded schema text (for tooling and tests).</summary>
    public static string SchemaText => ReadResource(SchemaResourceName);

    /// <summary>Loads and validates the catalogue embedded in this assembly.</summary>
    public static CatalogLoadResult LoadBuiltIn() => Load(BuiltInSources());

    /// <summary>
    /// Loads the built-in catalogue plus every <c>*.json</c> file under
    /// <paramref name="directory"/> (recursive). Directory entries override
    /// built-in entries with the same id; duplicates within the directory are errors.
    /// </summary>
    public static CatalogLoadResult LoadWithDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory))
        {
            return new CatalogLoadResult(null,
            [
                new CatalogIssue(CatalogIssueSeverity.Error, directory, string.Empty, "directory-missing",
                    $"Catalogue directory '{directory}' does not exist."),
            ]);
        }

        List<CatalogSource> sources = [.. BuiltInSources()];
        foreach (string file in Directory.EnumerateFiles(directory, "*" + JsonExtension, SearchOption.AllDirectories)
                     .Order(StringComparer.OrdinalIgnoreCase))
        {
            sources.Add(new CatalogSource(file, File.ReadAllText(file), CatalogOrigin.Override));
        }

        return Load(sources);
    }

    /// <summary>Loads and validates arbitrary sources (tests, custom hosts).</summary>
    public static CatalogLoadResult Load(IEnumerable<CatalogSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        List<CatalogIssue> issues = [];
        Dictionary<TweakId, (TweakDefinition Definition, CatalogSource Source)> merged = [];
        List<TweakId> order = [];

        foreach (CatalogSource source in sources)
        {
            IReadOnlyList<TweakDefinition>? entries = Parse(source, issues);
            if (entries is null)
            {
                continue;
            }

            foreach (TweakDefinition entry in entries)
            {
                if (merged.TryGetValue(entry.Id, out (TweakDefinition Definition, CatalogSource Source) existing))
                {
                    if (existing.Source.Origin < source.Origin)
                    {
                        // Operator override replaces the built-in entry.
                        merged[entry.Id] = (entry, source);
                        continue;
                    }

                    issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, source.Name, entry.Id.Value, "unique-id",
                        $"Duplicate id '{entry.Id}' (already defined in {existing.Source.Name})."));
                    continue;
                }

                merged[entry.Id] = (entry, source);
                order.Add(entry.Id);
            }
        }

        List<TweakDefinition> all = [.. order.Select(id => merged[id].Definition)];
        issues.AddRange(CatalogValidator.Validate(all, e => merged[e.Id].Source.Name));

        bool hasErrors = issues.Any(i => i.Severity == CatalogIssueSeverity.Error);
        return new CatalogLoadResult(hasErrors ? null : new TweakCatalog(all), issues);
    }

    /// <summary>Validates one document's JSON against the schema only (no structural rules).</summary>
    public static IReadOnlyList<CatalogIssue> ValidateSchema(CatalogSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        List<CatalogIssue> issues = [];
        using JsonDocument? document = ParseDocument(source, issues);
        if (document is not null)
        {
            EvaluateSchema(source, document.RootElement, issues);
        }

        return issues;
    }

    private static IEnumerable<CatalogSource> BuiltInSources()
        => ResourceNames().Keys
            .Where(n => n.StartsWith(EmbeddedPrefix, StringComparison.Ordinal)
                && n.EndsWith(JsonExtension, StringComparison.Ordinal)
                && !n.StartsWith(SchemaFolder, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(n => new CatalogSource(n, ReadResource(n), CatalogOrigin.BuiltIn));

    /// <summary>
    /// Normalised logical name (forward slashes) to the actual manifest name.
    /// MSBuild's %(RecursiveDir) yields backslashes on Windows, so the embedded
    /// names contain a backslash before the file name; callers use "catalog/privacy/file.json".
    /// </summary>
    private static Dictionary<string, string> ResourceNames()
        => typeof(CatalogLoader).Assembly.GetManifestResourceNames()
            .ToDictionary(n => n.Replace('\\', '/'), n => n, StringComparer.Ordinal);

    private static IReadOnlyList<TweakDefinition>? Parse(CatalogSource source, List<CatalogIssue> issues)
    {
        using JsonDocument? parsed = ParseDocument(source, issues);
        if (parsed is null || !EvaluateSchema(source, parsed.RootElement, issues))
        {
            return null;
        }

        try
        {
            CatalogDocument? document = JsonSerializer.Deserialize(source.Json, CatalogJsonContext.Default.CatalogDocument);
            if (document is null)
            {
                issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, source.Name, string.Empty, "json", "Document is null."));
                return null;
            }

            return document.Entries;
        }
        catch (JsonException ex)
        {
            issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, source.Name, ex.Path ?? string.Empty, "json", ex.Message));
            return null;
        }
    }

    private static JsonDocument? ParseDocument(CatalogSource source, List<CatalogIssue> issues)
    {
        try
        {
            return JsonDocument.Parse(source.Json, DocumentOptions);
        }
        catch (JsonException ex)
        {
            issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, source.Name, ex.Path ?? string.Empty, "json", ex.Message));
            return null;
        }
    }

    private static bool EvaluateSchema(CatalogSource source, JsonElement root, List<CatalogIssue> issues)
        => SchemaEvaluation.Validate(Schema.Value, root, source.Name, "catalogue schema", issues);

    private static JsonSchema LoadSchema() => JsonSchema.FromText(SchemaText);

    private static string ReadResource(string logicalName)
    {
        Assembly assembly = typeof(CatalogLoader).Assembly;
        string actualName = ResourceNames().GetValueOrDefault(logicalName.Replace('\\', '/'), logicalName);
        using Stream stream = assembly.GetManifestResourceStream(actualName)
            ?? throw new InvalidOperationException($"Embedded resource '{logicalName}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
