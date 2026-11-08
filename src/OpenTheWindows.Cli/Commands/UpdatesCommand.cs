using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Updates;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw updates pause --days &lt;n&gt; [--extended]</c>, <c>otw updates resume</c>,
/// <c>otw updates status</c>, <c>otw updates hidden</c>, <c>otw updates hide --kb</c>,
/// <c>otw updates unhide --kb</c>: the Windows Update guardrails. Pause, resume,
/// hide and unhide change machine state and need elevation; status and hidden are
/// reads. Exit codes: 0 success, 2 error, 3 unsupported OS, 4 invalid input,
/// 5 elevation required, 6 managed by policy (nothing changed).
/// </summary>
internal static class UpdatesCommand
{
    public static Command Create(CliServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var command = new Command("updates", "Pause, resume and inspect Windows Update (safe, reversible guardrails).");
        command.Subcommands.Add(CreatePause(services));
        command.Subcommands.Add(CreateResume(services));
        command.Subcommands.Add(CreateStatus(services));
        command.Subcommands.Add(CreateHidden(services));
        command.Subcommands.Add(CreateHide(services));
        command.Subcommands.Add(CreateUnhide(services));
        UpdatesMaintenanceCommands.AddTo(command, services);
        return command;
    }

    private static Command CreatePause(CliServices services)
    {
        var days = new Option<int>("--days") { Description = "Number of days to pause (1-35; up to 365 with --extended).", Required = true };
        var extended = new Option<bool>("--extended") { Description = "Allow a pause beyond Microsoft's 35-day cap (Advanced risk; writes Event 4002)." };
        var whatIf = new Option<bool>("--what-if") { Description = "Plan only; make no changes." };
        var command = new Command("pause", "Pause Windows Update for a number of days (writes the Settings-app pause values).");
        command.Options.Add(days);
        command.Options.Add(extended);
        command.Options.Add(whatIf);
        command.SetAction(parseResult => ExecutePause(parseResult, services, days, extended, whatIf));
        return command;
    }

    private static Command CreateResume(CliServices services)
    {
        var whatIf = new Option<bool>("--what-if") { Description = "Plan only; make no changes." };
        var command = new Command("resume", "Resume Windows Update: clear an Open the Windows pause and trigger a scan.");
        command.Options.Add(whatIf);
        command.SetAction(parseResult => ExecuteResume(parseResult, services, whatIf));
        return command;
    }

    private static Command CreateStatus(CliServices services)
    {
        var json = new Option<bool>("--json") { Description = "Emit the status as JSON on stdout." };
        var command = new Command("status", "Show the update state: version, days to end-of-servicing, pause and any pin.");
        command.Options.Add(json);
        command.SetAction(parseResult => ExecuteStatus(parseResult, services, json));
        return command;
    }

    private static bool TryBeginElevatedCommand(
        ParseResult parseResult, CliServices services, bool whatIf, string action,
        out TextWriter stdout, out TextWriter stderr, out int exitCode)
    {
        stdout = parseResult.InvocationConfiguration.Output;
        stderr = parseResult.InvocationConfiguration.Error;

        if (!CommandSupport.IsSupported(services.Doctor, stderr, out exitCode))
        {
            return false;
        }

        if (!whatIf && !services.Elevation.IsElevated)
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{action} updates needs an elevated token. Re-run from an elevated prompt."));
            exitCode = ExitCodes.ElevationRequired;
            return false;
        }

        return true;
    }

    private static int ExecutePause(ParseResult parseResult, CliServices services, Option<int> daysOption, Option<bool> extendedOption, Option<bool> whatIfOption)
    {
        bool whatIf = parseResult.GetValue(whatIfOption);
        if (!TryBeginElevatedCommand(parseResult, services, whatIf, "Pausing", out TextWriter stdout, out TextWriter stderr, out int exitCode))
        {
            return exitCode;
        }

        int days = parseResult.GetValue(daysOption);
        bool extended = parseResult.GetValue(extendedOption);

        PauseOutcome outcome;
        try
        {
            outcome = services.CreateUpdateControl().Pause(days, extended, whatIf);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            stderr.WriteLine(ex.Message);
            return ExitCodes.InvalidInput;
        }

        if (outcome.Extended)
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"WARNING: pausing for {outcome.Plan.Days} days exceeds Microsoft's supported 35-day limit; the device stays unpatched until you resume."));
        }

        if (outcome.Result.State is RunState.Failed or RunState.RolledBack)
        {
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Pause failed: {outcome.Result.State}."));
            return ExitCodes.Error;
        }

        // A run that skipped every value because policy manages Windows Update changed nothing:
        // say so rather than report a pause that never happened.
        if (outcome.Result.Entries.Count > 0 && outcome.Result.Entries.All(e => e.Result == ApplyState.Managed))
        {
            stdout.WriteLine("Windows Update is managed by MDM or Group Policy on this device; the pause was not applied.");
            return ExitCodes.Managed;
        }

        string verb = whatIf ? "Would pause" : "Paused";
        stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{verb} Windows Update for {outcome.Plan.Days} days, until {outcome.Plan.Expiry.UtcDateTime:yyyy-MM-dd HH:mm} UTC."));
        return ExitCodes.Success;
    }

    private static int ExecuteResume(ParseResult parseResult, CliServices services, Option<bool> whatIfOption)
    {
        bool whatIf = parseResult.GetValue(whatIfOption);
        if (!TryBeginElevatedCommand(parseResult, services, whatIf, "Resuming", out TextWriter stdout, out _, out int exitCode))
        {
            return exitCode;
        }

        ResumeOutcome outcome = services.CreateUpdateControl().Resume(whatIf);
        if (outcome.EndedPause is { } ended)
        {
            bool failed = ended.State is RunState.Failed or RunState.RolledBack;
            string endVerb = whatIf ? "Would end" : "Ended";
            stdout.WriteLine(failed
                ? "Resume failed: could not end the pause set in Settings."
                : string.Create(CultureInfo.InvariantCulture, $"{endVerb} the Windows Update pause set in Settings."));
            return failed ? ExitCodes.Error : ExitCodes.Success;
        }

        if (!outcome.WasPaused)
        {
            stdout.WriteLine("Updates are not paused; nothing to clear.");
            stdout.WriteLine(outcome.ScanTriggered ? "Triggered an update scan." : "Update scan not triggered.");
            return ExitCodes.Success;
        }

        if (outcome.Revert is { State: RunState.Failed })
        {
            stdout.WriteLine("Resume failed: could not clear the pause.");
            return ExitCodes.Error;
        }

        string verb = whatIf ? "Would resume" : "Resumed";
        stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{verb} Windows Update (cleared the pause)."));
        if (!whatIf)
        {
            stdout.WriteLine(outcome.ScanTriggered ? "Triggered an update scan." : "Update scan not triggered.");
        }

        return ExitCodes.Success;
    }

    private static int ExecuteStatus(ParseResult parseResult, CliServices services, Option<bool> jsonOption)
    {
        if (!CommandSupport.TryBeginSupported(services, parseResult, out TextWriter stdout, out _, out int exitCode))
        {
            return exitCode;
        }

        UpdateStatus status = services.CreateUpdateControl().Status();
        if (parseResult.GetValue(jsonOption))
        {
            stdout.WriteLine(JsonSerializer.Serialize(status, CliJsonContext.Default.UpdateStatus));
            return ExitCodes.Success;
        }

        stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Windows {status.CurrentVersion} ({status.EditionId}, {status.Family} servicing)"));

        if (status.EndOfServiceDate is { } eos && status.DaysUntilEndOfService is { } days)
        {
            string when = days < 0
                ? string.Create(CultureInfo.InvariantCulture, $"ended {-days} day(s) ago")
                : string.Create(CultureInfo.InvariantCulture, $"in {days} day(s)");
            string marker = status.CurrentVersionWithinWarning ? "  <-- WARNING" : string.Empty;
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"End of servicing: {eos:yyyy-MM-dd} ({when}){marker}"));
        }
        else
        {
            stdout.WriteLine("End of servicing: unknown for this version.");
        }

        stdout.WriteLine(status.IsPaused && status.PausedUntil is { } until
            ? string.Create(CultureInfo.InvariantCulture, $"Updates paused until {until.UtcDateTime:yyyy-MM-dd HH:mm} UTC.")
            : "Updates not paused.");

        if (status.TargetReleaseVersion is { } target)
        {
            string marker = status.TargetWithinWarning ? "  <-- WARNING" : string.Empty;
            string targetWhen = status.TargetDaysUntilEndOfService is { } targetDays
                ? string.Create(CultureInfo.InvariantCulture, $", end of servicing in {targetDays} day(s)")
                : string.Empty;
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Pinned to release {target}{targetWhen}{marker}"));
        }

        return ExitCodes.Success;
    }

    private static Command CreateHidden(CliServices services)
    {
        var json = new Option<bool>("--json") { Description = "Emit the hidden-update list as JSON." };
        var command = new Command("hidden", "List updates the Windows Update Agent currently has hidden.");
        command.Options.Add(json);
        command.SetAction(parseResult => ExecuteHidden(parseResult, services, json));
        return command;
    }

    // Shared by hide and unhide: one option instance is safe because each parse is separate.
    private static readonly Option<bool> HideWhatIf = new("--what-if") { Description = "Report what would change; change nothing (no elevation needed)." };

    private static Command CreateHide(CliServices services)
    {
        var kb = new Option<string>("--kb") { Description = "Knowledge-base id to hide (KB5063878 or 5063878).", Required = true };
        var json = new Option<bool>("--json") { Description = "Emit the outcome as JSON on stdout (stable schema)." };
        var command = new Command("hide", "Hide a discovered Windows update by knowledge-base id (reversible with unhide).");
        command.Options.Add(json);
        command.Options.Add(HideWhatIf);
        command.Options.Add(kb);
        command.SetAction(parseResult => ExecuteHide(parseResult, services, kb, json, hide: true));
        return command;
    }

    private static Command CreateUnhide(CliServices services)
    {
        var kb = new Option<string>("--kb") { Description = "Knowledge-base id to unhide (KB5063878 or 5063878).", Required = true };
        var json = new Option<bool>("--json") { Description = "Emit the outcome as JSON on stdout (stable schema)." };
        var command = new Command("unhide", "Clear the hidden flag on a discovered Windows update by knowledge-base id.");
        command.Options.Add(json);
        command.Options.Add(HideWhatIf);
        command.Options.Add(kb);
        command.SetAction(parseResult => ExecuteHide(parseResult, services, kb, json, hide: false));
        return command;
    }

    private static int ExecuteHidden(ParseResult parseResult, CliServices services, Option<bool> jsonOption)
    {
        TextWriter stdout = parseResult.InvocationConfiguration.Output;
        TextWriter stderr = parseResult.InvocationConfiguration.Error;
        if (!CommandSupport.IsSupported(services.Doctor, stderr, out int exitCode))
        {
            return exitCode;
        }

        IReadOnlyList<DiscoveredUpdate> hidden = services.CreateUpdateHide().ListHidden();
        if (parseResult.GetValue(jsonOption))
        {
            stdout.WriteLine(JsonSerializer.Serialize(hidden, CliJsonContext.Default.IReadOnlyListDiscoveredUpdate));
            return ExitCodes.Success;
        }

        if (hidden.Count == 0)
        {
            stdout.WriteLine("No hidden updates.");
            return ExitCodes.Success;
        }

        foreach (DiscoveredUpdate update in hidden)
        {
            string kbs = string.Join(", ", update.KnowledgeBaseIds.Select(KnowledgeBaseId.Format));
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{kbs}  {update.Title}"));
        }

        return ExitCodes.Success;
    }

    private static int ExecuteHide(ParseResult parseResult, CliServices services, Option<string> kbOption, Option<bool> jsonOption, bool hide)
    {
        bool whatIf = parseResult.GetValue(HideWhatIf);
        if (!TryBeginElevatedCommand(parseResult, services, whatIf, hide ? "Hiding" : "Unhiding", out TextWriter stdout, out TextWriter stderr, out int exitCode))
        {
            return exitCode;
        }

        string kb = parseResult.GetValue(kbOption) ?? string.Empty;
        if (KnowledgeBaseId.Normalize(kb) is null)
        {
            stderr.WriteLine("A knowledge-base id looks like KB5063878 or 5063878.");
            return ExitCodes.InvalidInput;
        }

        IUpdateHideService service = services.CreateUpdateHide();
        UpdateHideOutcome outcome;
        if (whatIf)
        {
            outcome = service.Preview(kb, hide);
        }
        else
        {
            outcome = hide ? service.Hide(kb) : service.Unhide(kb);
        }

        stdout.WriteLine(parseResult.GetValue(jsonOption)
            ? JsonSerializer.Serialize(outcome, CliJsonContext.Default.UpdateHideOutcome)
            : outcome.Message);
        return outcome.Succeeded ? ExitCodes.Success : ExitCodes.Error;
    }
}
