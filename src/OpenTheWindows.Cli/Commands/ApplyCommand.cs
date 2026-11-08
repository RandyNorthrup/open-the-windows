using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Profiles;

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

        var policy = ProfilePolicy.Read(services.CreateRegistryReader());
        if (!CommandSupport.TrySelectEntries(
            catalog, facts, parseResult.GetValue(options.Common.Profile), parseResult.GetValue(options.Common.Only),
            parseResult.GetValue(options.Common.IncludeDraft), policy, services.CreateTrustedKeys(), ExitCodes.InvalidInput, stderr,
            out IReadOnlyList<TweakDefinition> entries, out string profileName, out int selectExit, out Profile? selectedProfile))
        {
            return selectExit;
        }

        ApplyOptions applyOptions = ApplyReporting.BuildOptions(
            whatIf,
            parseResult.GetValue(options.NoRestorePoint),
            parseResult.GetValue(options.BreakGlass),
            parseResult.GetValue(options.AllowAdvanced),
            parseResult.GetValue(options.AllowBreaking),
            parseResult.GetValue(options.RestartExplorer),
            profileName,
            selectedProfile?.Options,
            selectedProfile?.Scope ?? Scope.Machine);

        ApplyEngine engine = services.CreateApplyEngine();

        bool json = parseResult.GetValue(options.Json);
        if (!whatIf && !ConfirmBreaking(engine, entries, applyOptions, parseResult.GetValue(options.Yes), json, stderr))
        {
            return ExitCodes.InvalidInput;
        }

        ApplyResult result = engine.Apply(entries, applyOptions);
        ApplyReporting.RegisterLogonFallback(services, selectedProfile, parseResult.GetValue(options.Common.Profile), applyOptions, result, whatIf, stderr);
        return Report(result, whatIf, json, stdout);
    }

    /// <summary>
    /// Asks for the id of every Breaking entry to be typed back. The prompt goes to stderr
    /// so a report on stdout stays clean; a <c>--json</c> run is non-interactive, so it must
    /// carry <c>--yes</c> instead of waiting for input nobody will type.
    /// </summary>
    private static bool ConfirmBreaking(
        ApplyEngine engine, IReadOnlyList<TweakDefinition> entries, ApplyOptions options, bool assumeYes, bool json, TextWriter stderr)
    {
        if (assumeYes || !options.AllowBreaking)
        {
            return true;
        }

        PlanResult plan = engine.Plan(entries, options);
        List<PlannedEntry> breaking = [.. plan.WillApply.Where(e => e.Risk == RiskTier.Breaking)];
        if (breaking.Count > 0 && json)
        {
            stderr.WriteLine("Breaking entries need confirmation; a --json run is non-interactive, so pass --yes to confirm them.");
            return false;
        }

        foreach (string id in breaking.Select(entry => entry.Id.Value))
        {
            stderr.Write(string.Create(CultureInfo.InvariantCulture, $"Breaking change '{id}'. Type the id to confirm: "));
            string? typed = Console.In.ReadLine();
            if (!string.Equals(typed, id, StringComparison.Ordinal))
            {
                stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Confirmation for '{id}' did not match; aborting."));
                return false;
            }
        }

        return true;
    }

    private static int Report(ApplyResult result, bool whatIf, bool json, TextWriter stdout)
    {
        if (json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(ApplyReporting.ToJson(result), CliJsonContext.Default.ApplyJsonReport));
        }
        else
        {
            ApplyReporting.WriteText(result, whatIf, stdout);
        }

        return ApplyReporting.ExitCode(result, whatIf);
    }
}
