using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw remediate --profile &lt;id|file&gt; [--include-draft] [--no-restore-point]
/// [--allow-advanced] [--allow-breaking] [--restart-explorer] [--what-if] [--json]
/// [--out &lt;file&gt;]</c>: unattended re-enforcement of a profile — scan, then apply
/// only the drifted entries, with no interactive confirmation. This is the entry
/// point the drift scheduled task and the Intune remediation script call; it never
/// break-glasses a managed setting. Writes the stable apply JSON report (to
/// <c>--out</c> when given, else stdout). With <c>--if-build-changed</c> it first
/// compares the running OS build/UBR against the most recent journal and does
/// nothing (exit 0) when the build is unchanged — this is the mode the drift
/// task's <c>--on-build-change</c> trigger uses to re-apply only after a feature
/// update. Exit codes: 0 compliant/applied/no-op, 2 error, 3 unsupported OS,
/// 4 invalid input, 5 elevation required, 3010 reboot required.
/// </summary>
internal static class RemediateCommand
{
    public static Command Create(CliServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new Options(
            CommandSupport.ProfileOption(),
            CommandSupport.CatalogDirOption(),
            CommandSupport.IncludeDraftOption(),
            new Option<bool>("--what-if") { Description = "Plan only; make no changes." },
            new Option<bool>("--no-restore-point") { Description = "Do not create a System Restore point first (a profile may also opt out)." },
            new Option<bool>("--allow-advanced") { Description = "Permit Advanced-risk entries (a profile's own option also enables these)." },
            new Option<bool>("--allow-breaking") { Description = "Permit Breaking-risk entries (a profile's own option also enables these)." },
            new Option<bool>("--restart-explorer") { Description = "Restart Explorer for entries that require it." },
            new Option<bool>("--if-build-changed") { Description = "Only re-apply when the OS build/UBR differs from the last journaled run (else exit 0 without changes)." },
            new Option<bool>("--json") { Description = "Emit the result as JSON on stdout (implied when --out is given)." },
            new Option<string?>("--out") { Description = "Write the JSON report to this file (e.g. the drift task's last-remediate.json)." });

        var command = new Command("remediate", "Unattended re-enforcement of a profile (scan, then apply drift). Used by the drift task and Intune.");
        options.AddTo(command);
        command.SetAction(parseResult => Execute(parseResult, services, options));
        return command;
    }

    private static int Execute(ParseResult parseResult, CliServices services, Options options)
    {
        TextWriter stdout = parseResult.InvocationConfiguration.Output;
        TextWriter stderr = parseResult.InvocationConfiguration.Error;

        if (!CommandSupport.TryResolveProfileCatalog(
            services, parseResult, options.CatalogDir, stderr,
            out TweakCatalog catalog, out OperatingSystemFacts facts, out int exitCode))
        {
            return exitCode;
        }

        bool whatIf = parseResult.GetValue(options.WhatIf);
        if (!whatIf && !services.Elevation.IsElevated)
        {
            stderr.WriteLine("Remediate needs an elevated token. Run it from an elevated prompt or a SYSTEM scheduled task.");
            return ExitCodes.ElevationRequired;
        }

        var policy = ProfilePolicy.Read(services.CreateRegistryReader());
        if (!CommandSupport.TrySelectEntries(
            catalog, facts, parseResult.GetValue(options.Profile), onlyId: null,
            parseResult.GetValue(options.IncludeDraft), policy, ProfileTrust.DefaultTrustStoreDirectory, ExitCodes.InvalidInput, stderr,
            out IReadOnlyList<TweakDefinition> entries, out string profileName, out int selectExit, out Profile? selectedProfile))
        {
            return selectExit;
        }

        if (parseResult.GetValue(options.IfBuildChanged))
        {
            BuildChangeStatus change = BuildChange.Detect(facts, LatestJournalOs(services));
            if (!change.Changed)
            {
                stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"OS build unchanged ({change.Current}); '{profileName}' not re-applied."));
                return ExitCodes.Success;
            }
        }

        // Unattended: a profile's own options seed the run and never break-glass a managed setting.
        ApplyOptions applyOptions = ApplyReporting.BuildOptions(
            whatIf,
            parseResult.GetValue(options.NoRestorePoint),
            breakGlass: false,
            parseResult.GetValue(options.AllowAdvanced),
            parseResult.GetValue(options.AllowBreaking),
            parseResult.GetValue(options.RestartExplorer),
            profileName,
            selectedProfile?.Options,
            selectedProfile?.Scope ?? Scope.Machine);

        ApplyResult result = services.CreateApplyEngine().Apply(entries, applyOptions);
        WriteReport(result, whatIf, parseResult.GetValue(options.Out), parseResult.GetValue(options.Json), stdout, stderr);
        return ApplyReporting.ExitCode(result, whatIf);
    }

    /// <summary>
    /// The OS recorded in the most recent journal, or <see langword="null"/> when
    /// there is no prior run to compare the live build against.
    /// </summary>
    private static JournalOs? LatestJournalOs(CliServices services)
    {
        IJournalStore journal = services.CreateJournalStore();
        IReadOnlyList<JournalSummary> history = journal.List();
        return history.Count == 0 ? null : journal.Load(history[0].RunId).Os;
    }

    /// <summary>
    /// Writes the run report: to <paramref name="outPath"/> as JSON when given
    /// (creating its directory), otherwise JSON or the text summary on stdout. A
    /// failed file write is reported but does not change the remediation's exit code
    /// — the machine state has already been decided.
    /// </summary>
    private static void WriteReport(ApplyResult result, bool whatIf, string? outPath, bool json, TextWriter stdout, TextWriter stderr)
    {
        if (outPath is null)
        {
            if (json)
            {
                stdout.WriteLine(JsonSerializer.Serialize(ApplyReporting.ToJson(result), CliJsonContext.Default.ApplyJsonReport));
            }
            else
            {
                ApplyReporting.WriteText(result, whatIf, stdout);
            }

            return;
        }

        try
        {
            string? directory = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outPath, JsonSerializer.Serialize(ApplyReporting.ToJson(result), CliJsonContext.Default.ApplyJsonReport));
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Wrote remediation report to {outPath}"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Remediation ran but the report could not be written to '{outPath}': {ex.Message}"));
        }
    }

    private sealed record Options(
        Option<string> Profile,
        Option<string?> CatalogDir,
        Option<bool> IncludeDraft,
        Option<bool> WhatIf,
        Option<bool> NoRestorePoint,
        Option<bool> AllowAdvanced,
        Option<bool> AllowBreaking,
        Option<bool> RestartExplorer,
        Option<bool> IfBuildChanged,
        Option<bool> Json,
        Option<string?> Out)
    {
        public void AddTo(Command command)
        {
            ArgumentNullException.ThrowIfNull(command);
            command.Options.Add(Profile);
            command.Options.Add(CatalogDir);
            command.Options.Add(IncludeDraft);
            command.Options.Add(WhatIf);
            command.Options.Add(NoRestorePoint);
            command.Options.Add(AllowAdvanced);
            command.Options.Add(AllowBreaking);
            command.Options.Add(RestartExplorer);
            command.Options.Add(IfBuildChanged);
            command.Options.Add(Json);
            command.Options.Add(Out);
        }
    }
}
