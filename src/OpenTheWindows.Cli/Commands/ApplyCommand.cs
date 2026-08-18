using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw apply --profile &lt;name&gt; [--include-draft] [--what-if]
/// [--no-restore-point] [--break-glass] [--allow-advanced] [--allow-breaking]
/// [--yes] [--restart-explorer] [--json]</c>: transactionally apply a built-in
/// level profile, journaling every prior state first and rolling back on any
/// failure. Exit codes: 0 applied, 3010 reboot required, 2 error, 3 unsupported
/// OS, 4 invalid input / conflict, 5 elevation required.
/// </summary>
internal static class ApplyCommand
{
    public static Command Create(CliServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new ApplyCommandOptions();
        var command = new Command("apply", "Transactionally apply a built-in level profile (journaled, reversible).");
        options.AddTo(command);
        command.SetAction(parseResult => Execute(parseResult, services, options));
        return command;
    }

    private static int Execute(ParseResult parseResult, CliServices services, ApplyCommandOptions options)
    {
        TextWriter stdout = parseResult.InvocationConfiguration.Output;
        TextWriter stderr = parseResult.InvocationConfiguration.Error;

        if (!CommandSupport.TryResolveProfileCatalog(
            services, parseResult, options.Common.CatalogDir, stderr,
            out TweakCatalog catalog, out OperatingSystemFacts facts, out int exitCode))
        {
            return exitCode;
        }

        bool whatIf = parseResult.GetValue(options.WhatIf);
        if (!whatIf && !services.Elevation.IsElevated)
        {
            stderr.WriteLine("Apply needs an elevated token. Re-run from an elevated prompt.");
            return ExitCodes.ElevationRequired;
        }

        if (!CommandSupport.TrySelectEntries(
            catalog, facts, parseResult.GetValue(options.Common.Profile), parseResult.GetValue(options.Common.Only),
            parseResult.GetValue(options.Common.IncludeDraft), ExitCodes.InvalidInput, stderr,
            out IReadOnlyList<TweakDefinition> entries, out string profileName, out int selectExit))
        {
            return selectExit;
        }

        var applyOptions = new ApplyOptions(
            whatIf,
            !parseResult.GetValue(options.NoRestorePoint),
            parseResult.GetValue(options.BreakGlass),
            parseResult.GetValue(options.AllowAdvanced),
            parseResult.GetValue(options.AllowBreaking),
            profileName,
            parseResult.GetValue(options.RestartExplorer));

        ApplyEngine engine = services.CreateApplyEngine();

        if (!whatIf && !ConfirmBreaking(engine, entries, applyOptions, parseResult.GetValue(options.Yes), stdout, stderr))
        {
            return ExitCodes.InvalidInput;
        }

        ApplyResult result = engine.Apply(entries, applyOptions);
        return Report(result, whatIf, parseResult.GetValue(options.Json), stdout);
    }

    private static bool ConfirmBreaking(
        ApplyEngine engine, IReadOnlyList<TweakDefinition> entries, ApplyOptions options, bool assumeYes, TextWriter stdout, TextWriter stderr)
    {
        if (assumeYes || !options.AllowBreaking)
        {
            return true;
        }

        PlanResult plan = engine.Plan(entries, options);
        foreach (PlannedEntry entry in plan.WillApply)
        {
            if (entry.Risk != RiskTier.Breaking)
            {
                continue;
            }

            stdout.Write(string.Create(CultureInfo.InvariantCulture, $"Breaking change '{entry.Id.Value}'. Type the id to confirm: "));
            string? typed = Console.In.ReadLine();
            if (!string.Equals(typed, entry.Id.Value, StringComparison.Ordinal))
            {
                stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Confirmation for '{entry.Id.Value}' did not match; aborting."));
                return false;
            }
        }

        return true;
    }

    private static int Report(ApplyResult result, bool whatIf, bool json, TextWriter stdout)
    {
        if (json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(ToJson(result), CliJsonContext.Default.ApplyJsonReport));
        }
        else
        {
            WriteText(result, whatIf, stdout);
        }

        if (result.Plan.HasErrors)
        {
            return ExitCodes.InvalidInput;
        }

        if (result.State == RunState.Failed)
        {
            return result.Preflight.Blocking.Any(i => i.Check == PreflightCheck.Elevation)
                ? ExitCodes.ElevationRequired
                : ExitCodes.Error;
        }

        if (result.State == RunState.RolledBack)
        {
            return ExitCodes.Error;
        }

        // A --what-if run changes nothing, so it never itself requires a reboot: the
        // preview succeeded. The would-be reboot requirement is still reported in the
        // text/JSON output; only an actual apply returns the 3010 reboot-required code.
        if (whatIf)
        {
            return ExitCodes.Success;
        }

        return result.Requires == RestartRequirement.Reboot ? ExitCodes.RebootRequired : ExitCodes.Success;
    }

    private static void WriteText(ApplyResult result, bool whatIf, TextWriter stdout)
    {
        if (result.Plan.HasErrors)
        {
            foreach (string error in result.Plan.Errors)
            {
                stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  ERROR  {error}"));
            }

            return;
        }

        foreach (PreflightIssue issue in result.Preflight.Blocking)
        {
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  BLOCKED  {issue.Check}: {issue.Detail}"));
        }

        string verb = whatIf ? "Would apply" : "Applied";
        foreach (EntryOutcome entry in result.Entries)
        {
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {entry.Result,-13} {entry.Id.Value}{Suffix(entry.Note)}"));
        }

        stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{verb}: {result.State}. Restart requirement: {result.Requires}. Restore point: {result.RestorePoint.Status}."));
    }

    private static string Suffix(string? note) => note is null ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"  ({note})");

    private static ApplyJsonReport ToJson(ApplyResult result)
        => new(
            result.RunId.ToString(),
            result.State.ToString(),
            result.Requires.ToString(),
            new ApplyJsonRestore(result.RestorePoint.Status.ToString(), result.RestorePoint.SequenceNumber),
            result.Plan.Errors,
            [.. result.Entries.Select(e => new ApplyJsonEntry(e.Id.Value, e.Result.ToString(), e.Risk.ToString(), e.Requires.ToString(), e.Note))]);
}
