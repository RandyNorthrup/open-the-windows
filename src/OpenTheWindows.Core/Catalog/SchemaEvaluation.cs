using System.Text.Json;
using Json.Schema;

namespace OpenTheWindows.Core.Catalog;

/// <summary>
/// Shared JSON Schema evaluation for catalogue and profile documents. Uses the
/// cheap pass/fail evaluation first and only builds the expensive per-location
/// detail output when a document is invalid, then maps each violation to a
/// <see cref="CatalogIssue"/>.
/// </summary>
internal static class SchemaEvaluation
{
    // Detailed output (per-location errors) is only built when a document is
    // invalid; the common valid case uses the much cheaper pass/fail evaluation.
    private static readonly EvaluationOptions DetailedOptions = new() { OutputFormat = OutputFormat.List };

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="root"/> conforms to
    /// <paramref name="schema"/>; otherwise appends one <see cref="CatalogIssue"/>
    /// per violation (rule <c>schema</c>) and returns <see langword="false"/>.
    /// </summary>
    /// <param name="schema">Compiled schema to evaluate against.</param>
    /// <param name="root">The document root element.</param>
    /// <param name="source">Source name (logical resource name or file path) for the findings.</param>
    /// <param name="schemaLabel">Human label for the fallback message, e.g. "catalogue schema".</param>
    /// <param name="issues">Findings accumulator.</param>
    public static bool Validate(JsonSchema schema, JsonElement root, string source, string schemaLabel, List<CatalogIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(issues);

        if (schema.Evaluate(root).IsValid)
        {
            return true;
        }

        EvaluationResults results = schema.Evaluate(root, DetailedOptions);

        bool reported = false;
        foreach (EvaluationResults detail in results.Details ?? [])
        {
            if (detail.IsValid || detail.Errors is not { Count: > 0 })
            {
                continue;
            }

            foreach ((string keyword, string message) in detail.Errors)
            {
                reported = true;
                issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, source, detail.InstanceLocation.ToString(),
                    "schema", $"{keyword}: {message}"));
            }
        }

        if (!reported)
        {
            issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, source, string.Empty, "schema",
                $"Document does not conform to the {schemaLabel}."));
        }

        return false;
    }
}
