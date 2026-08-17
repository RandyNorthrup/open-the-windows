using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw revert &lt;runId|last&gt; [--what-if] [--restart-explorer] [--json]</c>:
/// restore the machine to the state captured before a prior run, journaling a new
/// revert run. Exit codes: 0 reverted, 2 error, 3 unsupported OS, 4 invalid run
/// id, 5 elevation required.
/// </summary>
internal static class RevertCommand
{
    public static Command Create(CliServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var runArgument = new Argument<string>("run") { Description = "The run id (GUID) to revert, or 'last' for the most recent run." };
        var whatIf = new Option<bool>("--what-if") { Description = "Plan only; make no changes." };
        var restartExplorer = new Option<bool>("--restart-explorer") { Description = "Restart Explorer for entries that require it." };
        var json = new Option<bool>("--json") { Description = "Emit the result as JSON on stdout (stable schema)." };

        var command = new Command("revert", "Restore the state captured before a prior run (journaled).");
        command.Arguments.Add(runArgument);
        command.Options.Add(whatIf);
        command.Options.Add(restartExplorer);
        command.Options.Add(json);
        command.SetAction(parseResult => Execute(parseResult, services, runArgument, whatIf, restartExplorer, json));
        return command;
    }

    private static int Execute(
        ParseResult parseResult, CliServices services,
        Argument<string> runArgument, Option<bool> whatIfOption, Option<bool> restartExplorerOption, Option<bool> jsonOption)
    {
        TextWriter stdout = parseResult.InvocationConfiguration.Output;
        TextWriter stderr = parseResult.InvocationConfiguration.Error;

        if (!CommandSupport.IsSupported(services.Doctor, stderr, out int unsupported))
        {
            return unsupported;
        }

        bool whatIf = parseResult.GetValue(whatIfOption);
        if (!whatIf && !services.Elevation.IsElevated)
        {
            stderr.WriteLine("Revert needs an elevated token. Re-run from an elevated prompt.");
            return ExitCodes.ElevationRequired;
        }

        ApplyEngine engine = services.CreateApplyEngine();
        string token = parseResult.GetValue(runArgument) ?? string.Empty;
        if (!TryResolveRun(engine, token, out Guid runId, out string error))
        {
            stderr.WriteLine(error);
            return ExitCodes.InvalidInput;
        }

        RevertResult result = engine.Revert(runId, new RevertOptions(whatIf, parseResult.GetValue(restartExplorerOption)));

        if (parseResult.GetValue(jsonOption))
        {
            stdout.WriteLine(JsonSerializer.Serialize(ToJson(result), CliJsonContext.Default.ApplyJsonReport));
        }
        else
        {
            WriteText(result, whatIf, stdout);
        }

        return result.State is RunState.Completed ? ExitCodes.Success : ExitCodes.Error;
    }

    private static bool TryResolveRun(ApplyEngine engine, string token, out Guid runId, out string error)
    {
        error = string.Empty;
        if (string.Equals(token, "last", StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyList<JournalSummary> history = engine.History();
            if (history.Count == 0)
            {
                runId = Guid.Empty;
                error = "No prior runs to revert.";
                return false;
            }

            runId = history[0].RunId;
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
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {entry.Result,-13} {entry.Id.Value}"));
        }

        stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{verb} run {result.RevertedRunId}: {result.State}."));
    }

    private static ApplyJsonReport ToJson(RevertResult result)
        => new(
            result.RunId.ToString(),
            result.State.ToString(),
            OpenTheWindows.Core.Model.RestartRequirement.None.ToString(),
            new ApplyJsonRestore(Core.Abstractions.RestorePointStatus.NotRequested.ToString(), null),
            [],
            [.. result.Entries.Select(e => new ApplyJsonEntry(e.Id.Value, e.Result.ToString(), e.Risk.ToString(), e.Requires.ToString(), e.Note))]);
}
