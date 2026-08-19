using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Audit;
using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw audit</c>: score the machine against the built-in security baselines and
/// validate baseline files. Read-only; never touches the machine.
/// Exit codes: 0 ok; 4 invalid input (bad baseline, unknown id).
/// </summary>
internal static class AuditCommand
{
    public static Command Create(CliServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var command = new Command("audit", "Score the machine against security baselines and validate baseline files (read-only).");
        command.Subcommands.Add(CreateValidate(services));
        return command;
    }

    private static Command CreateValidate(CliServices services)
    {
        var catalogDirOption = new Option<string?>("--catalog-dir")
        {
            Description = "Directory of additional catalogue JSON files; entries override built-in ones by id.",
        };
        var baselineDirOption = new Option<string?>("--baseline-dir")
        {
            Description = "Directory of additional baseline JSON files; baselines override built-in ones by id.",
        };
        var jsonOption = new Option<bool>("--json") { Description = "Emit JSON on stdout (stable schema)." };

        var command = new Command("validate",
            "Validate the built-in baselines (plus --baseline-dir) against the schema, the catalogue tweak ids and the health-check ids.");
        command.Options.Add(catalogDirOption);
        command.Options.Add(baselineDirOption);
        command.Options.Add(jsonOption);
        command.SetAction(parseResult => ExecuteValidate(parseResult, services, catalogDirOption, baselineDirOption, jsonOption));
        return command;
    }

    private static int ExecuteValidate(
        ParseResult parseResult,
        CliServices services,
        Option<string?> catalogDirOption,
        Option<string?> baselineDirOption,
        Option<bool> jsonOption)
    {
        TextWriter stdout = parseResult.InvocationConfiguration.Output;

        CatalogLoadResult catalogResult = services.LoadCatalog(parseResult.GetValue(catalogDirOption));
        string? baselineDir = parseResult.GetValue(baselineDirOption);
        BaselineSetLoadResult set = baselineDir is null
            ? BaselineLoader.LoadBuiltIn()
            : BaselineLoader.LoadWithDirectory(baselineDir);

        List<CatalogIssue> issues = [.. set.Issues];
        if (catalogResult.Catalog is { } catalog)
        {
            foreach (Baseline baseline in set.Baselines)
            {
                issues.AddRange(BaselineValidator.Validate(baseline, baseline.Id, catalog, HealthCheckIds.All));
            }
        }
        else
        {
            issues.AddRange(catalogResult.Errors);
            issues.Add(new CatalogIssue(CatalogIssueSeverity.Error, "catalogue", string.Empty, "catalog-invalid",
                "Catalogue failed to load, so baseline tweak-id references were not checked. Run 'otw catalog validate' for details."));
        }

        int errorCount = issues.Count(i => i.Severity == CatalogIssueSeverity.Error);
        int warningCount = issues.Count(i => i.Severity == CatalogIssueSeverity.Warning);
        bool valid = errorCount == 0;
        int ruleCount = set.Baselines.Sum(b => b.Rules.Count);
        var report = new BaselineValidationReport(valid, set.Baselines.Count, ruleCount, issues);

        if (parseResult.GetValue(jsonOption))
        {
            stdout.WriteLine(JsonSerializer.Serialize(report, BaselineJsonContext.Default.BaselineValidationReport));
        }
        else
        {
            foreach (CatalogIssue issue in issues)
            {
                stdout.WriteLine(issue.ToString());
            }

            stdout.WriteLine(valid
                ? string.Create(CultureInfo.InvariantCulture, $"Baselines valid: {set.Baselines.Count} baseline(s), {ruleCount} rule(s), {warningCount} warning(s).")
                : string.Create(CultureInfo.InvariantCulture, $"Baselines INVALID: {errorCount} error(s), {warningCount} warning(s)."));
        }

        return valid ? ExitCodes.Success : ExitCodes.InvalidInput;
    }
}
