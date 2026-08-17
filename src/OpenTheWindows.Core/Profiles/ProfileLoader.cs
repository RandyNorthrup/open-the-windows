using System.Reflection;
using System.Text.Json;
using Json.Schema;
using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Profiles;

/// <summary>
/// Loads profile documents (the embedded built-ins or an operator file),
/// validates each against the profile JSON Schema, and produces a
/// <see cref="Profile"/>. Never throws for profile content problems; they are
/// reported as <see cref="CatalogIssue"/>s on the <see cref="ProfileLoadResult"/>.
/// Structural checks that need the catalogue (unknown include/exclude ids,
/// built-ins never Breaking) live in <see cref="ProfileValidator"/>.
/// </summary>
public static class ProfileLoader
{
    /// <summary>Logical name of the embedded profile JSON Schema.</summary>
    public const string SchemaResourceName = "catalog/schema/profile.schema.json";

    /// <summary>Logical name prefix of the embedded built-in profile files.</summary>
    public const string EmbeddedPrefix = "profiles/";

    private const string JsonExtension = ".json";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly Lazy<JsonSchema> Schema = new(LoadSchema);

    /// <summary>The embedded schema text (for tooling and tests).</summary>
    public static string SchemaText => ReadResource(SchemaResourceName);

    /// <summary>Ids of the embedded built-in profiles, sorted.</summary>
    public static IReadOnlyList<string> BuiltInIds
        => [.. EmbeddedNames().Select(NameToId).Order(StringComparer.Ordinal)];

    /// <summary>Loads and validates every embedded built-in profile, sorted by file name.</summary>
    public static IReadOnlyList<ProfileLoadResult> LoadBuiltIns()
        => [.. EmbeddedNames()
            .Order(StringComparer.Ordinal)
            .Select(name => LoadText(name, ReadResource(name)))];

    /// <summary>
    /// Loads and validates the embedded built-in profile whose id is
    /// <paramref name="id"/>. Returns <see langword="null"/> when no built-in has
    /// that id.
    /// </summary>
    public static ProfileLoadResult? TryLoadBuiltIn(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        string resource = EmbeddedPrefix + id + JsonExtension;
        return EmbeddedNames().Contains(resource, StringComparer.Ordinal)
            ? LoadText(resource, ReadResource(resource))
            : null;
    }

    /// <summary>Loads and validates a profile from a file on disk.</summary>
    public static ProfileLoadResult LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            return new ProfileLoadResult(null,
            [
                new CatalogIssue(CatalogIssueSeverity.Error, path, string.Empty, "file-missing",
                    $"Profile file '{path}' does not exist."),
            ]);
        }

        return LoadText(path, File.ReadAllText(path));
    }

    /// <summary>Loads and validates a profile from its JSON text.</summary>
    public static ProfileLoadResult LoadText(string sourceName, string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentNullException.ThrowIfNull(json);

        List<CatalogIssue> issues = [];
        JsonDocument? document = ParseDocument(sourceName, json, issues);
        if (document is null)
        {
            return new ProfileLoadResult(null, issues);
        }

        using (document)
        {
            if (!SchemaEvaluation.Validate(Schema.Value, document.RootElement, sourceName, "profile schema", issues))
            {
                return new ProfileLoadResult(null, issues);
            }
        }

        try
        {
            Profile? profile = JsonSerializer.Deserialize(json, ProfileJsonContext.Default.Profile);
            if (profile is null)
            {
                issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, sourceName, string.Empty, "json", "Profile document is null."));
                return new ProfileLoadResult(null, issues);
            }

            if (profile.SchemaVersion != Profile.CurrentSchemaVersion)
            {
                issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, sourceName, "/schemaVersion", "schema-version",
                    $"Unsupported profile schemaVersion {profile.SchemaVersion}; this build understands {Profile.CurrentSchemaVersion}."));
                return new ProfileLoadResult(null, issues);
            }

            return new ProfileLoadResult(profile, issues);
        }
        catch (JsonException ex)
        {
            issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, sourceName, ex.Path ?? string.Empty, "json", ex.Message));
            return new ProfileLoadResult(null, issues);
        }
    }

    private static JsonDocument? ParseDocument(string sourceName, string json, List<CatalogIssue> issues)
    {
        try
        {
            return JsonDocument.Parse(json, DocumentOptions);
        }
        catch (JsonException ex)
        {
            issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, sourceName, ex.Path ?? string.Empty, "json", ex.Message));
            return null;
        }
    }

    private static IEnumerable<string> EmbeddedNames()
        => ResourceNames().Keys
            .Where(n => n.StartsWith(EmbeddedPrefix, StringComparison.Ordinal)
                && n.EndsWith(JsonExtension, StringComparison.Ordinal));

    private static string NameToId(string resourceName)
        => resourceName[EmbeddedPrefix.Length..^JsonExtension.Length];

    private static Dictionary<string, string> ResourceNames()
        => typeof(ProfileLoader).Assembly.GetManifestResourceNames()
            .ToDictionary(n => n.Replace('\\', '/'), n => n, StringComparer.Ordinal);

    private static JsonSchema LoadSchema() => JsonSchema.FromText(SchemaText);

    private static string ReadResource(string logicalName)
    {
        Assembly assembly = typeof(ProfileLoader).Assembly;
        string actualName = ResourceNames().GetValueOrDefault(logicalName.Replace('\\', '/'), logicalName);
        using Stream stream = assembly.GetManifestResourceStream(actualName)
            ?? throw new InvalidOperationException($"Embedded resource '{logicalName}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
