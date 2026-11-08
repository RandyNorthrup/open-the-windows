using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Updates;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw updates target</c> (hold feature updates at a version, or clear the hold) and
/// <c>otw updates repair</c> (the supervised reset of a stuck Windows Update client).
/// Both need elevation. Exit codes: 0 success, 2 error, 3 unsupported OS, 4 invalid input,
/// 5 elevation required, 6 managed by policy (nothing changed).
/// </summary>
internal static class UpdatesMaintenanceCommands
{
    /// <summary>Adds the <c>target</c> and <c>repair</c> subcommands to <paramref name="updates"/>.</summary>
    public static void AddTo(Command updates, CliServices services)
    {
        ArgumentNullException.ThrowIfNull(updates);
        ArgumentNullException.ThrowIfNull(services);
        updates.Subcommands.Add(CreateTarget(services));
        updates.Subcommands.Add(CreateRepair(services));
    }

    private static Command CreateTarget(CliServices services)
    {
        var version = new Option<string?>("--version") { Description = "The Windows 11 version to hold at, for example 24H2 (see --list)." };
        var clear = new Option<bool>("--clear") { Description = "Remove the hold so newer feature updates are offered again." };
        var list = new Option<bool>("--list") { Description = "List the versions this device can be held at." };
        var whatIf = new Option<bool>("--what-if") { Description = "Plan only; make no changes." };
        var json = new Option<bool>("--json") { Description = "Emit the result as JSON on stdout." };
        var command = new Command("target", "Hold feature updates at a Windows 11 version (security updates keep installing), or clear the hold.");
        command.Options.Add(version);
        command.Options.Add(clear);
        command.Options.Add(list);
        command.Options.Add(whatIf);
        command.Options.Add(json);
        command.SetAction(parseResult => ExecuteTarget(parseResult, services, new TargetOptions(version, clear, list, whatIf, json)));
        return command;
    }

    private static int ExecuteTarget(ParseResult parseResult, CliServices services, TargetOptions options)
    {
        TextWriter stdout = parseResult.InvocationConfiguration.Output;
        TextWriter stderr = parseResult.InvocationConfiguration.Error;
        UpdateTargetService target = services.CreateUpdateTarget();
        if (parseResult.GetValue(options.List))
        {
            stdout.WriteLine(string.Join(Environment.NewLine, target.KnownVersions()));
            return ExitCodes.Success;
        }

        string? version = parseResult.GetValue(options.Version);
        bool clear = parseResult.GetValue(options.ClearHold);
        if (clear == !string.IsNullOrWhiteSpace(version))
        {
            stderr.WriteLine("Give either --version <version> or --clear.");
            return ExitCodes.InvalidInput;
        }

        bool whatIf = parseResult.GetValue(options.WhatIf);
        if (!CanRun(services, whatIf, "Changing the feature-update hold", stderr, out int gateExit))
        {
            return gateExit;
        }

        return clear
            ? Clear(target, whatIf, stdout)
            : Hold(target, version!, whatIf, parseResult.GetValue(options.Json), stdout, stderr);
    }

    private static int Hold(UpdateTargetService target, string version, bool whatIf, bool json, TextWriter stdout, TextWriter stderr)
    {
        ApplyResult result;
        try
        {
            result = target.Target(version, whatIf);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            stderr.WriteLine(ex.Message);
            return ExitCodes.InvalidInput;
        }

        if (json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(ApplyReporting.ToJson(result), CliJsonContext.Default.ApplyJsonReport));
        }
        else if (result.Entries.All(e => e.Result == ApplyState.Managed))
        {
            stdout.WriteLine("Windows Update is managed by MDM or Group Policy on this device; the hold was not applied.");
        }
        else
        {
            ApplyReporting.WriteText(result, whatIf, stdout);
        }

        return result.Entries.Count > 0 && result.Entries.All(e => e.Result == ApplyState.Managed)
            ? ExitCodes.Managed
            : ApplyReporting.ExitCode(result, whatIf);
    }

    private static int Clear(UpdateTargetService target, bool whatIf, TextWriter stdout)
    {
        IReadOnlyList<RevertResult> reverts = target.Clear(whatIf);
        if (reverts.Count == 0)
        {
            stdout.WriteLine("No feature-update hold set by Open the Windows is active.");
            return ExitCodes.Success;
        }

        foreach (RevertResult revert in reverts)
        {
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{(whatIf ? "Would clear" : "Cleared")} hold {revert.RevertedRunId}: {revert.State}."));
        }

        return reverts.All(r => r.State == RunState.Completed) ? ExitCodes.Success : ExitCodes.Error;
    }

    private static Command CreateRepair(CliServices services)
    {
        var whatIf = new Option<bool>("--what-if") { Description = "List the steps without changing anything." };
        var json = new Option<bool>("--json") { Description = "Emit the result as JSON on stdout." };
        var command = new Command(
            "repair",
            "Reset a stuck Windows Update client: stop its services for the repair only, set the download cache and catroot2 aside, start them again.");
        command.Options.Add(whatIf);
        command.Options.Add(json);
        command.SetAction(parseResult => ExecuteRepair(parseResult, services, whatIf, json));
        return command;
    }

    private static int ExecuteRepair(ParseResult parseResult, CliServices services, Option<bool> whatIfOption, Option<bool> jsonOption)
    {
        TextWriter stdout = parseResult.InvocationConfiguration.Output;
        TextWriter stderr = parseResult.InvocationConfiguration.Error;
        bool whatIf = parseResult.GetValue(whatIfOption);
        if (!CanRun(services, whatIf, "Repairing Windows Update", stderr, out int gateExit))
        {
            return gateExit;
        }

        UpdateRepairResult result = services.CreateUpdateRepair().Repair(whatIf);
        if (parseResult.GetValue(jsonOption))
        {
            stdout.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.UpdateRepairResult));
        }
        else
        {
            foreach (string step in result.Steps)
            {
                stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {step}"));
            }

            if (result.Error is not null)
            {
                stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Repair stopped: {result.Error}"));
            }
        }

        return result.Succeeded ? ExitCodes.Success : ExitCodes.Error;
    }

    /// <summary>The platform must be supported; a real change also needs elevation, a what-if does not.</summary>
    private static bool CanRun(CliServices services, bool whatIf, string action, TextWriter stderr, out int exitCode)
    {
        if (!CommandSupport.IsSupported(services.Doctor, stderr, out exitCode))
        {
            return false;
        }

        if (!whatIf && !services.Elevation.IsElevated)
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{action} needs an elevated token. Re-run from an elevated prompt."));
            exitCode = ExitCodes.ElevationRequired;
            return false;
        }

        return true;
    }

    private sealed record TargetOptions(
        Option<string?> Version, Option<bool> ClearHold, Option<bool> List, Option<bool> WhatIf, Option<bool> Json);
}
