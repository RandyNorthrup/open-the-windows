using System.CommandLine;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Audit;
using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw audit run|list|validate</c>: score the machine against a security
/// baseline, list the built-in baselines, and validate baseline files. Read-only;
/// never touches the machine (audit does not require elevation, though some health
/// checks report Unknown without it).
/// Exit codes: 0 ok / no failures, 1 failures found, 2 error, 3 unsupported OS,
/// 4 invalid input (unknown or invalid baseline).
/// </summary>
internal static class AuditCommand
{
    public static Command Create(CliServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var command = new Command("audit", "Score the machine against security baselines and validate baseline files (read-only).");
        command.Subcommands.Add(CreateRun(services));
        command.Subcommands.Add(CreateList());
        command.Subcommands.Add(CreateValidate(services));
        return command;
    }

    // ----- run ---------------------------------------------------------------

    private static Command CreateRun(CliServices services)
    {
        var options = new RunOptions();
        var command = new Command("run", "Audit this machine against a baseline (read-only) and write the report.");
        options.AddTo(command);
        command.SetAction(parseResult => ExecuteRun(parseResult, services, options));
        return command;
    }

    private static int ExecuteRun(ParseResult parseResult, CliServices services, RunOptions options)
    {
        TextWriter stdout = parseResult.InvocationConfiguration.Output;
        TextWriter stderr = parseResult.InvocationConfiguration.Error;

        if (!CommandSupport.TryResolveProfileCatalog(
            services, parseResult, options.CatalogDir, stderr, out TweakCatalog catalog, out _, out int exitCode))
        {
            return exitCode;
        }

        if (!TryLoadBaseline(parseResult.GetValue(options.Baseline) ?? string.Empty, stderr, out Baseline baseline))
        {
            return ExitCodes.InvalidInput;
        }

        IReadOnlyList<CatalogIssue> issues = BaselineValidator.Validate(baseline, baseline.Id, catalog, HealthCheckIds.All);
        if (issues.Any(i => i.Severity == CatalogIssueSeverity.Error))
        {
            foreach (CatalogIssue issue in issues.Where(i => i.Severity == CatalogIssueSeverity.Error))
            {
                stderr.WriteLine(issue.ToString());
            }

            stderr.WriteLine("The baseline is invalid; run 'otw audit validate' for details.");
            return ExitCodes.InvalidInput;
        }

        if (options.FormatCount(parseResult) > 1)
        {
            stderr.WriteLine("Choose at most one of --json, --csv, --html, --sarif.");
            return ExitCodes.Error;
        }

        string? signPath = parseResult.GetValue(options.Sign);
        if (signPath is not null && (parseResult.GetValue(options.Csv) || parseResult.GetValue(options.Html) || parseResult.GetValue(options.Sarif)))
        {
            stderr.WriteLine("Signing (--sign) is only available with the JSON format.");
            return ExitCodes.Error;
        }

        using ECDsa? key = signPath is null ? null : CommandSupport.LoadEcKey(signPath, stderr);
        if (signPath is not null && key is null)
        {
            return ExitCodes.InvalidInput;
        }

        AuditReport report = new AuditEngine(services.CreateScanEngine(), services.Health, catalog).Audit(baseline);

        string? outPath = parseResult.GetValue(options.Out);
        IAuditReportWriter? writer = ResolveWriter(parseResult, options, key, outPath);
        if (writer is not null)
        {
            CommandSupport.EmitReport(writer.Extension, outPath, stdout, stream => writer.Write(report, stream));
        }
        else
        {
            WriteSummary(stdout, report);
        }

        return report.Summary.Fail == 0 ? ExitCodes.Success : ExitCodes.Drift;
    }

    private static IAuditReportWriter? ResolveWriter(ParseResult parseResult, RunOptions options, ECDsa? key, string? outPath)
    {
        if (parseResult.GetValue(options.Csv))
        {
            return new AuditCsvWriter();
        }

        if (parseResult.GetValue(options.Html))
        {
            return new AuditHtmlWriter();
        }

        if (parseResult.GetValue(options.Sarif))
        {
            return new AuditSarifWriter();
        }

        // JSON when asked, when signing, or when writing to a file; otherwise a text summary.
        return parseResult.GetValue(options.Json) || key is not null || outPath is not null
            ? new AuditJsonWriter(key)
            : null;
    }

    private static bool TryLoadBaseline(string value, TextWriter stderr, out Baseline baseline)
    {
        baseline = null!;
        BaselineLoadResult? result = File.Exists(value) ? BaselineLoader.LoadFile(value) : BaselineLoader.TryLoadBuiltIn(value);
        if (result is null)
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Unknown baseline '{value}'. Use a built-in id ({string.Join(", ", BaselineLoader.BuiltInIds)}) or a path to a baseline .json file."));
            return false;
        }

        if (result.Baseline is null)
        {
            foreach (CatalogIssue issue in result.Errors)
            {
                stderr.WriteLine(issue.ToString());
            }

            return false;
        }

        baseline = result.Baseline;
        return true;
    }

    private static void WriteSummary(TextWriter stdout, AuditReport report)
    {
        AuditSummary summary = report.Summary;
        stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{report.BaselineTitle} ({report.BaselineId}) on {report.Os.ProductName} build {report.Os.FullBuild}"));
        stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Score: {report.Score} / 100"));
        stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{summary.Total} rule(s): {summary.Pass} pass, {summary.Fail} fail, {summary.NotApplicable} n/a, {summary.Manual} manual, {summary.Unknown} unknown."));

        foreach (AuditRuleResult result in report.Results)
        {
            if (result.Outcome == AuditOutcome.Fail)
            {
                stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  FAIL  [{BaselineSeverityText.Label(result.Severity)}] {result.RuleId}  {result.Title}"));
            }
        }
    }

    // ----- list --------------------------------------------------------------

    private static Command CreateList()
    {
        var jsonOption = new Option<bool>("--json") { Description = "Emit JSON on stdout (stable schema)." };
        var command = new Command("list", "List the built-in security baselines.");
        command.Options.Add(jsonOption);
        command.SetAction(parseResult =>
        {
            TextWriter stdout = parseResult.InvocationConfiguration.Output;
            List<BaselineSummary> summaries = [.. BaselineLoader.LoadBuiltIn().Baselines
                .Select(b => new BaselineSummary(b.Id, b.Title, b.Rules.Count))];

            if (parseResult.GetValue(jsonOption))
            {
                stdout.WriteLine(JsonSerializer.Serialize<IReadOnlyList<BaselineSummary>>(summaries, BaselineJsonContext.Default.IReadOnlyListBaselineSummary));
                return ExitCodes.Success;
            }

            int idWidth = Math.Max(2, summaries.Count == 0 ? 0 : summaries.Max(s => s.Id.Length));
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{"ID".PadRight(idWidth)}  RULES  TITLE"));
            foreach (BaselineSummary summary in summaries)
            {
                stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{summary.Id.PadRight(idWidth)}  {summary.RuleCount,5}  {summary.Title}"));
            }

            return ExitCodes.Success;
        });
        return command;
    }

    // ----- validate ----------------------------------------------------------

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

    private sealed class RunOptions
    {
        public Option<string> Baseline { get; } = new("--baseline")
        {
            Description = "Baseline id (see 'otw audit list') or a path to a baseline .json file.",
            Required = true,
        };

        public Option<string?> CatalogDir { get; } = new("--catalog-dir")
        {
            Description = "Directory of additional catalogue JSON files; entries override built-in ones by id.",
        };

        public Option<bool> Json { get; } = new("--json") { Description = "Write a JSON audit report (with an integrity hash; add --sign to sign it)." };

        public Option<bool> Csv { get; } = new("--csv") { Description = "Write a CSV audit report (one row per rule)." };

        public Option<bool> Html { get; } = new("--html") { Description = "Write a self-contained HTML audit report (score, summary and per-severity tables)." };

        public Option<bool> Sarif { get; } = new("--sarif") { Description = "Write a SARIF 2.1.0 audit report (rule ids are the baseline rule ids)." };

        public Option<string?> Out { get; } = new("--out") { Description = "Write the audit report to this file instead of stdout." };

        public Option<string?> Sign { get; } = new("--sign") { Description = "Sign the JSON report with the ES256 private key in this PEM file." };

        public void AddTo(Command command)
        {
            command.Options.Add(Baseline);
            command.Options.Add(CatalogDir);
            command.Options.Add(Json);
            command.Options.Add(Csv);
            command.Options.Add(Html);
            command.Options.Add(Sarif);
            command.Options.Add(Out);
            command.Options.Add(Sign);
        }

        public int FormatCount(ParseResult parseResult)
            => (parseResult.GetValue(Json) ? 1 : 0)
                + (parseResult.GetValue(Csv) ? 1 : 0)
                + (parseResult.GetValue(Html) ? 1 : 0)
                + (parseResult.GetValue(Sarif) ? 1 : 0);
    }
}
