using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw revert &lt;runId|last&gt; [--entry &lt;id&gt;]... [--what-if] [--force] [--restart-explorer] [--json]</c>
/// or <c>otw revert --all</c>: restore the machine to the state captured before a prior
/// run (or only some of its entries), journaling a new revert run; <c>--all</c> undoes
/// every run this product made, newest first. The journal must verify (signature, hash and owner), and the revert is
/// refused when the run was already reverted or a later run changed the same settings,
/// unless <c>--force</c>. Exit codes: 0 reverted, 2 error or refused, 3 unsupported OS,
/// 4 invalid run id, 5 elevation required.
/// </summary>
internal static class RevertCommand
{
    public static Command Create(CliServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var runArgument = new Argument<string>("run")
        {
            Description = "The run id (GUID) to revert, or 'last' for the most recent run (omit with --all).",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var entry = new Option<string[]>("--entry") { Description = "Revert only this entry of the run (repeatable).", AllowMultipleArgumentsPerToken = false };
        var all = new Option<bool>("--all") { Description = "Undo everything: revert every run this product made that is not already reverted, newest first." };
        var whatIf = new Option<bool>("--what-if") { Description = "Plan only; make no changes." };
        var force = new Option<bool>("--force")
        {
            Description = "Revert even if the run was already reverted or a later run changed the same settings (overwriting them).",
        };
        var restartExplorer = new Option<bool>("--restart-explorer") { Description = "Restart Explorer for entries that require it." };
        var json = new Option<bool>("--json") { Description = "Emit the result as JSON on stdout (stable schema)." };
        var command = new Command("revert", "Restore the state captured before a prior run (journaled).");
        command.Arguments.Add(runArgument);
        command.Options.Add(entry);
        command.Options.Add(all);
        command.Options.Add(whatIf);
        command.Options.Add(force);
        command.Options.Add(restartExplorer);
        command.Options.Add(json);
        command.SetAction(parseResult => Execute(parseResult, services, runArgument, new RevertFlags(whatIf, force, restartExplorer, json, entry, all)));
        return command;
    }

    private static int Execute(ParseResult parseResult, CliServices services, Argument<string> runArgument, RevertFlags flags)
    {
        TextWriter stdout = parseResult.InvocationConfiguration.Output;
        TextWriter stderr = parseResult.InvocationConfiguration.Error;
        if (!CommandSupport.IsSupported(services.Doctor, stderr, out int unsupported))
        {
            return unsupported;
        }

        bool whatIf = parseResult.GetValue(flags.WhatIf);
        if (!whatIf && !services.Elevation.IsElevated)
        {
            stderr.WriteLine("Revert needs an elevated token. Re-run from an elevated prompt.");
            return ExitCodes.ElevationRequired;
        }

        ApplyEngine engine = services.CreateApplyEngine();
        string[] entries = parseResult.GetValue(flags.Entry) ?? [];
        var options = new RevertOptions(
            whatIf, parseResult.GetValue(flags.RestartExplorer), parseResult.GetValue(flags.Force), entries.Length == 0 ? null : entries);
        string? token = parseResult.GetValue(runArgument);
        if (parseResult.GetValue(flags.All))
        {
            if (token is not null || entries.Length > 0)
            {
                stderr.WriteLine("--all reverts whole runs; do not combine it with a run id or --entry.");
                return ExitCodes.InvalidInput;
            }

            return RevertAll(engine, options, stdout, stderr);
        }

        return RevertOne(engine, token ?? string.Empty, options, parseResult.GetValue(flags.Json), stdout, stderr);
    }

    private static int RevertOne(ApplyEngine engine, string token, RevertOptions options, bool json, TextWriter stdout, TextWriter stderr)
    {
        if (!TryResolveRun(engine, token, out Guid runId, out string error))
        {
            stderr.WriteLine(error);
            return ExitCodes.InvalidInput;
        }

        RevertResult result;
        try
        {
            result = engine.Revert(runId, options);
        }
        catch (FileNotFoundException ex)
        {
            stderr.WriteLine(ex.Message);
            return ExitCodes.InvalidInput;
        }
        catch (JournalIntegrityException ex)
        {
            stderr.WriteLine(ex.Message);
            return ExitCodes.Error;
        }

        if (json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(ToJson(result), CliJsonContext.Default.ApplyJsonReport));
        }
        else
        {
            WriteText(result, options.WhatIf, stdout);
        }

        return result.State is RunState.Completed ? ExitCodes.Success : ExitCodes.Error;
    }

    /// <summary>
    /// Resolves the run token: a GUID, or <c>last</c> for the newest run whose journal
    /// verifies (a damaged or tampered journal is never picked implicitly).
    /// </summary>
    private static bool TryResolveRun(ApplyEngine engine, string token, out Guid runId, out string error)
    {
        error = string.Empty;
        if (string.Equals(token, "last", StringComparison.OrdinalIgnoreCase))
        {
            JournalSummary? newest = engine.History().FirstOrDefault(s => s.Integrity == JournalIntegrity.Verified);
            if (newest is null)
            {
                runId = Guid.Empty;
                error = "No prior runs with a verified journal to revert.";
                return false;
            }

            runId = newest.RunId;
            return true;
        }

        if (Guid.TryParse(token, out runId))
        {
            return true;
        }

        error = string.Create(CultureInfo.InvariantCulture, $"'{token}' is not a run id (GUID) or 'last'.");
        return false;
    }

    private static void WriteText(RevertResult result, bool whatIf, TextWriter stdout)
    {
        string verb = whatIf ? "Would revert" : "Reverted";
        foreach (EntryOutcome entry in result.Entries)
        {
            string note = entry.Note is null ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"  ({entry.Note})");
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {entry.Result,-13} {entry.Id.Value}{note}"));
        }

        bool refused = result.Entries.Count > 0 && result.Entries.All(e => e.Result == ApplyState.Refused);
        stdout.WriteLine(refused
            ? string.Create(CultureInfo.InvariantCulture, $"Revert of run {result.RevertedRunId} refused; nothing was changed.")
            : string.Create(CultureInfo.InvariantCulture, $"{verb} run {result.RevertedRunId}: {result.State}."));
    }

    private static ApplyJsonReport ToJson(RevertResult result)
        => new(
            result.RunId.ToString(),
            result.State.ToString(),
            OpenTheWindows.Core.Model.RestartRequirement.None.ToString(),
            new ApplyJsonRestore(Core.Abstractions.RestorePointStatus.NotRequested.ToString(), null),
            [],
            [.. result.Entries.Select(e => new ApplyJsonEntry(e.Id.Value, e.Result.ToString(), e.Risk.ToString(), e.Requires.ToString(), e.Note))]);

    /// <summary>
    /// Reverts every apply run that is not already fully reverted, newest first, so later
    /// changes are unwound before the earlier ones they built on. A run already reverted is
    /// skipped; any other refusal or failure is reported and makes the exit code non-zero.
    /// </summary>
    private static int RevertAll(ApplyEngine engine, RevertOptions options, TextWriter stdout, TextWriter stderr)
    {
        List<JournalSummary> runs = [.. engine.History().Where(s =>
            s.Integrity == JournalIntegrity.Verified && s.RevertOf is null && s.Result is RunState.Completed or RunState.Failed)];
        int problems = 0;
        foreach (JournalSummary run in runs)
        {
            RevertResult result;
            try
            {
                result = engine.Revert(run.RunId, options);
            }
            catch (JournalIntegrityException ex)
            {
                stderr.WriteLine(ex.Message);
                problems++;
                continue;
            }

            if (result.AlreadyReverted)
            {
                continue;
            }

            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{(options.WhatIf ? "Would revert" : "Reverted")} {run.RunId} ({run.Profile}): {result.State}."));
            problems += result.State == RunState.Completed ? 0 : 1;
        }

        stdout.WriteLine(problems == 0 ? "Every change this product made has been reverted." : string.Create(CultureInfo.InvariantCulture, $"{problems} run(s) could not be reverted; see above."));
        return problems == 0 ? ExitCodes.Success : ExitCodes.Error;
    }

    private sealed record RevertFlags(
        Option<bool> WhatIf, Option<bool> Force, Option<bool> RestartExplorer, Option<bool> Json, Option<string[]> Entry, Option<bool> All);
}
