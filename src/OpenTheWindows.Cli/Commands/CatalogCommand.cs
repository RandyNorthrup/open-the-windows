using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw catalog list|show|validate</c>: inspect and validate the tweak
/// catalogue. Read-only; never touches the machine.
/// Exit codes: 0 ok; 4 invalid input (bad catalogue, unknown id, unknown filter).
/// </summary>
internal static class CatalogCommand
{
    private const string IdColumnHeader = "ID";

    public static Command Create(Func<string?, CatalogLoadResult> loadCatalog)
    {
        ArgumentNullException.ThrowIfNull(loadCatalog);

        var catalogDirOption = new Option<string?>("--catalog-dir")
        {
            Description = "Directory of additional catalogue JSON files; entries override built-in ones by id.",
            Recursive = true,
        };
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Emit JSON on stdout (stable schema).",
            Recursive = true,
        };

        var command = new Command("catalog", "Inspect and validate the tweak catalogue (read-only).");
        command.Options.Add(catalogDirOption);
        command.Options.Add(jsonOption);
        command.Subcommands.Add(CreateList(loadCatalog, catalogDirOption, jsonOption));
        command.Subcommands.Add(CreateShow(loadCatalog, catalogDirOption, jsonOption));
        command.Subcommands.Add(CreateValidate(loadCatalog, catalogDirOption, jsonOption));
        return command;
    }

    private static Command CreateList(Func<string?, CatalogLoadResult> loadCatalog, Option<string?> catalogDir, Option<bool> json)
    {
        var categoryOption = new Option<Category?>("--category") { Description = "Only entries in this category." };
        var levelOption = new Option<Level?>("--level") { Description = "Only entries at or below this level (Basic..Paranoid)." };
        var command = new Command("list", "List catalogue entries.");
        command.Options.Add(categoryOption);
        command.Options.Add(levelOption);
        command.SetAction(parseResult =>
        {
            TextWriter stdout = parseResult.InvocationConfiguration.Output;
            TextWriter stderr = parseResult.InvocationConfiguration.Error;
            CatalogLoadResult result = loadCatalog(parseResult.GetValue(catalogDir));
            if (!TryGetCatalog(result, stderr, out TweakCatalog? catalog))
            {
                return ExitCodes.InvalidInput;
            }

            IEnumerable<TweakDefinition> entries = catalog.Entries;
            if (parseResult.GetValue(categoryOption) is Category category)
            {
                entries = entries.Where(e => e.Category == category);
            }

            if (parseResult.GetValue(levelOption) is Level level)
            {
                entries = entries.Where(e => e.Level <= level);
            }

            var selected = new List<TweakDefinition>(entries);
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonSerializer.Serialize<IReadOnlyList<TweakDefinition>>(selected, CatalogJsonContext.Default.IReadOnlyListTweakDefinition));
                return ExitCodes.Success;
            }

            WriteTable(stdout, selected);
            return ExitCodes.Success;
        });
        return command;
    }

    private static Command CreateShow(Func<string?, CatalogLoadResult> loadCatalog, Option<string?> catalogDir, Option<bool> json)
    {
        var idArgument = new Argument<string>("id") { Description = "Tweak id, e.g. privacy.advertising-id.disable" };
        var command = new Command("show", "Show one catalogue entry with its actions and sources.");
        command.Arguments.Add(idArgument);
        command.SetAction(parseResult =>
        {
            TextWriter stdout = parseResult.InvocationConfiguration.Output;
            TextWriter stderr = parseResult.InvocationConfiguration.Error;
            string idText = parseResult.GetValue(idArgument) ?? string.Empty;
            if (!TweakId.IsValid(idText))
            {
                stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"'{idText}' is not a valid tweak id."));
                return ExitCodes.InvalidInput;
            }

            CatalogLoadResult result = loadCatalog(parseResult.GetValue(catalogDir));
            if (!TryGetCatalog(result, stderr, out TweakCatalog? catalog))
            {
                return ExitCodes.InvalidInput;
            }

            if (!catalog.TryGet(new TweakId(idText), out TweakDefinition? entry))
            {
                stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"No catalogue entry '{idText}'."));
                return ExitCodes.InvalidInput;
            }

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonSerializer.Serialize(entry, CatalogJsonContext.Default.TweakDefinition));
                return ExitCodes.Success;
            }

            WriteEntry(stdout, entry);
            return ExitCodes.Success;
        });
        return command;
    }

    private static Command CreateValidate(Func<string?, CatalogLoadResult> loadCatalog, Option<string?> catalogDir, Option<bool> json)
    {
        var command = new Command("validate", "Validate the built-in catalogue (plus --catalog-dir) against the schema and structural rules.");
        command.SetAction(parseResult =>
        {
            TextWriter stdout = parseResult.InvocationConfiguration.Output;
            CatalogLoadResult result = loadCatalog(parseResult.GetValue(catalogDir));
            var report = CatalogValidationReport.From(result);

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonSerializer.Serialize(report, CatalogJsonContext.Default.CatalogValidationReport));
            }
            else
            {
                foreach (CatalogIssue issue in report.Issues)
                {
                    stdout.WriteLine(issue.ToString());
                }

                stdout.WriteLine(report.IsValid
                    ? string.Create(CultureInfo.InvariantCulture, $"Catalogue valid: {report.EntryCount} entries, {result.Warnings.Count()} warning(s).")
                    : string.Create(CultureInfo.InvariantCulture, $"Catalogue INVALID: {result.Errors.Count()} error(s), {result.Warnings.Count()} warning(s)."));
            }

            return report.IsValid ? ExitCodes.Success : ExitCodes.InvalidInput;
        });
        return command;
    }

    private static bool TryGetCatalog(CatalogLoadResult result, TextWriter stderr, out TweakCatalog catalog)
    {
        if (result.Catalog is null)
        {
            foreach (CatalogIssue issue in result.Errors)
            {
                stderr.WriteLine(issue.ToString());
            }

            stderr.WriteLine("Catalogue failed validation; run 'otw catalog validate' for details.");
            catalog = null!;
            return false;
        }

        catalog = result.Catalog;
        return true;
    }

    private static void WriteTable(TextWriter writer, List<TweakDefinition> entries)
    {
        int idWidth = Math.Max(IdColumnHeader.Length, entries.Count == 0 ? 0 : entries.Max(e => e.Id.Value.Length));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{IdColumnHeader.PadRight(idWidth)}  {"CATEGORY",-11} {"LEVEL",-8} {"RISK",-8} {"STATUS",-10} TITLE"));
        foreach (TweakDefinition e in entries)
        {
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{e.Id.Value.PadRight(idWidth)}  {e.Category,-11} {e.Level,-8} {e.Risk,-8} {e.Status,-10} {e.Title}"));
        }

        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{entries.Count} entries."));
    }

    private static void WriteEntry(TextWriter writer, TweakDefinition e)
    {
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{e.Id}  [{e.Category} / {e.Level} / {e.Risk} / {e.Scope} / {e.Status}]"));
        writer.WriteLine(e.Title);
        writer.WriteLine();
        writer.WriteLine(e.Description);
        writer.WriteLine();
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Why: {e.Rationale}"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Requires: {e.Requires}"));
        if (e.AppliesTo.Editions.Count > 0 || e.AppliesTo.MinBuild is not null || e.AppliesTo.MaxBuild is not null)
        {
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Applies to: editions [{string.Join(", ", e.AppliesTo.Editions)}], builds {e.AppliesTo.MinBuild?.ToString(CultureInfo.InvariantCulture) ?? "*"}..{e.AppliesTo.MaxBuild?.ToString(CultureInfo.InvariantCulture) ?? "*"}"));
        }

        writer.WriteLine("Actions:");
        foreach (Core.Catalog.Actions.ITweakAction action in e.Actions)
        {
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  - {action.Kind}: {action}"));
        }

        if (e.SideEffects.Count > 0)
        {
            writer.WriteLine("Side effects:");
            foreach (string effect in e.SideEffects)
            {
                writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  - {effect}"));
            }
        }

        if (e.ManagedBy is not null)
        {
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Managed by: CSP {e.ManagedBy.PolicyCsp ?? "-"}; GPO {e.ManagedBy.GroupPolicyPath ?? "-"}"));
        }

        writer.WriteLine("Sources:");
        foreach (Uri source in e.Sources)
        {
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  - {source}"));
        }

        writer.WriteLine(e.VerifiedOn.Count == 0
            ? "Verified on: (not yet verified)"
            : string.Create(CultureInfo.InvariantCulture, $"Verified on: {string.Join("; ", e.VerifiedOn.Select(v => $"{v.Build} {v.Edition} {v.Date:yyyy-MM-dd}"))}"));
    }
}
