using System.Reflection;
using System.Text.Json;
using Json.Schema;
using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Audit;

/// <summary>
/// Loads baseline documents (the embedded built-ins or operator files), validates
/// each against the baseline JSON Schema, and produces <see cref="Baseline"/>s.
/// Never throws for content problems; they are reported as
/// <see cref="CatalogIssue"/>s. Structural checks that need the catalogue (unknown
/// tweak ids, unknown health-check ids, source rules) live in
/// <see cref="BaselineValidator"/>, mirroring how <c>ProfileLoader</c> and
/// <c>ProfileValidator</c> divide the work.
/// </summary>
public static class BaselineLoader
{
    /// <summary>Logical name of the embedded baseline JSON Schema.</summary>
    public const string SchemaResourceName = "catalog/schema/baseline.schema.json";

    /// <summary>Logical name prefix of the embedded built-in baseline files.</summary>
    public const string EmbeddedPrefix = "baselines/";

    private const string JsonExtension = ".json";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly Lazy<JsonSchema> Schema = new(LoadSchema);

    private static Assembly Assembly => typeof(BaselineLoader).Assembly;

    /// <summary>The embedded schema text (for tooling and tests).</summary>
    public static string SchemaText => EmbeddedResources.Read(Assembly, SchemaResourceName);

    /// <summary>Ids of the embedded built-in baselines, sorted.</summary>
    public static IReadOnlyList<string> BuiltInIds
        => [.. EmbeddedNames().Select(NameToId).Order(StringComparer.Ordinal)];

    /// <summary>Loads and validates every embedded built-in baseline, sorted by id.</summary>
    public static BaselineSetLoadResult LoadBuiltIn()
        => LoadSet(EmbeddedNames().Order(StringComparer.Ordinal).Select(name => (name, EmbeddedResources.Read(Assembly, name))));

    /// <summary>
    /// Loads the built-in baselines plus every <c>*.json</c> file under
    /// <paramref name="directory"/> (recursive). A directory baseline whose id
    /// matches a built-in replaces it; a bad or unparseable file is reported.
    /// </summary>
    public static BaselineSetLoadResult LoadWithDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory))
        {
            return new BaselineSetLoadResult([],
            [
                new CatalogIssue(CatalogIssueSeverity.Error, directory, string.Empty, "directory-missing",
                    $"Baseline directory '{directory}' does not exist."),
            ]);
        }

        List<(string Name, string Json)> sources =
            [.. EmbeddedNames().Order(StringComparer.Ordinal).Select(name => (name, EmbeddedResources.Read(Assembly, name)))];
        foreach (string file in Directory.EnumerateFiles(directory, "*" + JsonExtension, SearchOption.AllDirectories)
                     .Order(StringComparer.OrdinalIgnoreCase))
        {
            sources.Add((file, File.ReadAllText(file)));
        }

        return LoadSet(sources);
    }

    /// <summary>
    /// Loads and validates the embedded built-in baseline whose id is
    /// <paramref name="id"/>. Returns <see langword="null"/> when no built-in has that id.
    /// </summary>
    public static BaselineLoadResult? TryLoadBuiltIn(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        string resource = EmbeddedPrefix + id + JsonExtension;
        return EmbeddedNames().Contains(resource, StringComparer.Ordinal)
            ? LoadText(resource, EmbeddedResources.Read(Assembly, resource))
            : null;
    }

    /// <summary>Loads and validates a baseline from a file on disk.</summary>
    public static BaselineLoadResult LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return new BaselineLoadResult(null,
            [
                new CatalogIssue(CatalogIssueSeverity.Error, path, string.Empty, "file-missing",
                    $"Baseline file '{path}' does not exist."),
            ]);
        }

        return LoadText(path, File.ReadAllText(path));
    }

    /// <summary>Loads and validates a baseline from its JSON text.</summary>
    public static BaselineLoadResult LoadText(string sourceName, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentNullException.ThrowIfNull(json);

        List<CatalogIssue> issues = [];
        JsonDocument? document = EmbeddedResources.Parse(sourceName, json, DocumentOptions, issues);
        if (document is null)
        {
            return new BaselineLoadResult(null, issues);
        }

        using (document)
        {
            if (!SchemaEvaluation.Validate(Schema.Value, document.RootElement, sourceName, "baseline schema", issues))
            {
                return new BaselineLoadResult(null, issues);
            }
        }

        try
        {
            Baseline? baseline = JsonSerializer.Deserialize(json, BaselineJsonContext.Default.Baseline);
            if (baseline is null)
            {
                issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, sourceName, string.Empty, "json", "Baseline document is null."));
                return new BaselineLoadResult(null, issues);
            }

            if (baseline.SchemaVersion != Baseline.CurrentSchemaVersion)
            {
                issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, sourceName, "/schemaVersion", "schema-version",
                    $"Unsupported baseline schemaVersion {baseline.SchemaVersion}; this build understands {Baseline.CurrentSchemaVersion}."));
                return new BaselineLoadResult(null, issues);
            }

            return new BaselineLoadResult(baseline, issues);
        }
        catch (JsonException ex)
        {
            issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, sourceName, ex.Path ?? string.Empty, "json", ex.Message));
            return new BaselineLoadResult(null, issues);
        }
    }

    private static BaselineSetLoadResult LoadSet(IEnumerable<(string Name, string Json)> sources)
    {
        List<CatalogIssue> issues = [];
        Dictionary<string, Baseline> byId = [];
        List<string> order = [];

        foreach ((string name, string json) in sources)
        {
            BaselineLoadResult result = LoadText(name, json);
            issues.AddRange(result.Issues);
            if (result.Baseline is null)
            {
                continue;
            }

            if (!byId.ContainsKey(result.Baseline.Id))
            {
                order.Add(result.Baseline.Id);
            }

            byId[result.Baseline.Id] = result.Baseline;
        }

        List<Baseline> baselines = [.. order.Select(id => byId[id]).OrderBy(b => b.Id, StringComparer.Ordinal)];
        return new BaselineSetLoadResult(baselines, issues);
    }

    private static IEnumerable<string> EmbeddedNames()
        => EmbeddedResources.ManifestNames(Assembly).Keys
            .Where(n => n.StartsWith(EmbeddedPrefix, StringComparison.Ordinal)
                && n.EndsWith(JsonExtension, StringComparison.Ordinal));

    private static string NameToId(string resourceName)
        => resourceName[EmbeddedPrefix.Length..^JsonExtension.Length];

    private static JsonSchema LoadSchema() => JsonSchema.FromText(SchemaText);
}
