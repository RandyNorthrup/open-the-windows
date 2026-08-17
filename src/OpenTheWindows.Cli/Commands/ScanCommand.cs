using System.CommandLine;
using System.Globalization;
using System.Text;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Reports;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw scan --profile &lt;name&gt; [--catalog-dir] [--include-draft]
/// [--json|--csv|--html|--sarif] [--out &lt;file&gt;]</c>: read-only drift scan of
/// a built-in level profile against the machine. Never changes anything.
/// Exit codes: 0 compliant, 1 drift, 2 error, 3 unsupported OS.
/// </summary>
internal static class ScanCommand
{
    public static Command Create(CliServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new ScanOptions();
        var command = new Command("scan", "Read-only drift scan of a built-in level profile against this machine.");
        options.AddTo(command);
        command.SetAction(parseResult => Execute(parseResult, services, options));
        return command;
    }

    private static int Execute(ParseResult parseResult, CliServices services, ScanOptions options)
    {
        TextWriter stdout = parseResult.InvocationConfiguration.Output;
        TextWriter stderr = parseResult.InvocationConfiguration.Error;

        if (!CommandSupport.TryResolveProfileCatalog(
            services, parseResult, options.Profile, options.CatalogDir, ExitCodes.Error, stderr,
            out Level level, out TweakCatalog catalog, out int exitCode))
        {
            return exitCode;
        }

        IReportWriter? writer = options.ResolveWriter(parseResult, out int formatCount);
        if (formatCount > 1)
        {
            stderr.WriteLine("Choose at most one of --json, --csv, --html, --sarif.");
            return ExitCodes.Error;
        }

        string profileName = parseResult.GetValue(options.Profile)!;
        IReadOnlyList<TweakDefinition> entries = NamedProfile.Select(catalog, level, parseResult.GetValue(options.IncludeDraft));
        ScanReport report = services.CreateScanEngine().Scan(entries, profileName);

        string? outPath = parseResult.GetValue(options.Out);
        writer ??= outPath is not null ? new JsonReportWriter() : null;

        if (writer is not null)
        {
            WriteReport(writer, report, outPath, stdout);
        }
        else
        {
            WriteSummary(stdout, report);
        }

        return report.IsCompliant ? ExitCodes.Success : ExitCodes.Drift;
    }

    private static void WriteReport(IReportWriter writer, ScanReport report, string? outPath, TextWriter stdout)
    {
        if (outPath is not null)
        {
            using FileStream file = File.Create(outPath);
            writer.Write(report, file);
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Wrote {writer.Extension} report to {outPath}"));
            return;
        }

        using var buffer = new MemoryStream();
        writer.Write(report, buffer);
        stdout.Write(Encoding.UTF8.GetString(buffer.ToArray()));
        stdout.WriteLine();
    }

    private static void WriteSummary(TextWriter stdout, ScanReport report)
    {
        stdout.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Profile '{report.ProfileName}' on {report.Os.ProductName} build {report.Os.FullBuild}"));
        stdout.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{report.Results.Count} entr(y/ies): {report.CompliantCount} compliant, {report.DriftCount} drift, {report.ManagedCount} managed."));

        foreach (TweakObservation observation in report.Results)
        {
            if (observation.State == ComplianceState.Drift)
            {
                stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  DRIFT  {observation.Id.Value}"));
            }
        }

        stdout.WriteLine(report.IsCompliant ? "Compliant." : "Drift detected.");
    }

    private sealed class ScanOptions
    {
        public Option<string> Profile { get; } = CommandSupport.ProfileOption();

        public Option<string?> CatalogDir { get; } = CommandSupport.CatalogDirOption();

        public Option<bool> IncludeDraft { get; } = CommandSupport.IncludeDraftOption();

        public Option<bool> Json { get; } = new("--json") { Description = "Write a JSON report." };

        public Option<bool> Csv { get; } = new("--csv") { Description = "Write a CSV report." };

        public Option<bool> Html { get; } = new("--html") { Description = "Write a self-contained HTML report." };

        public Option<bool> Sarif { get; } = new("--sarif") { Description = "Write a SARIF 2.1.0 report." };

        public Option<string?> Out { get; } = new("--out") { Description = "Write the report to this file instead of stdout." };

        public void AddTo(Command command)
        {
            command.Options.Add(Profile);
            command.Options.Add(CatalogDir);
            command.Options.Add(IncludeDraft);
            command.Options.Add(Json);
            command.Options.Add(Csv);
            command.Options.Add(Html);
            command.Options.Add(Sarif);
            command.Options.Add(Out);
        }

        public IReportWriter? ResolveWriter(ParseResult parseResult, out int formatCount)
        {
            IReportWriter? writer = null;
            formatCount = 0;

            if (parseResult.GetValue(Json))
            {
                writer = new JsonReportWriter();
                formatCount++;
            }

            if (parseResult.GetValue(Csv))
            {
                writer = new CsvReportWriter();
                formatCount++;
            }

            if (parseResult.GetValue(Html))
            {
                writer = new HtmlReportWriter();
                formatCount++;
            }

            if (parseResult.GetValue(Sarif))
            {
                writer = new SarifReportWriter();
                formatCount++;
            }

            return writer;
        }
    }
}
