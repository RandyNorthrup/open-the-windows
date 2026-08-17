using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw history [--json]</c>: list prior apply and revert runs from the
/// journal, newest first. Read-only; exit code 0 unless the command errors.
/// </summary>
internal static class HistoryCommand
{
    public static Command Create(CliServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var json = new Option<bool>("--json") { Description = "Emit the history as JSON on stdout (stable schema)." };
        var command = new Command("history", "List prior apply and revert runs from the journal (newest first).");
        command.Options.Add(json);
        command.SetAction(parseResult =>
        {
            TextWriter stdout = parseResult.InvocationConfiguration.Output;
            IReadOnlyList<JournalSummary> runs = services.CreateApplyEngine().History();

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonSerializer.Serialize(runs, CliJsonContext.Default.IReadOnlyListJournalSummary));
                return ExitCodes.Success;
            }

            if (runs.Count == 0)
            {
                stdout.WriteLine("No runs recorded.");
                return ExitCodes.Success;
            }

            foreach (JournalSummary run in runs)
            {
                string kind = run.RevertOf is { } reverted
                    ? string.Create(CultureInfo.InvariantCulture, $"revert of {reverted}")
                    : "apply";
                stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"{run.StartedAt:u}  {run.RunId}  {run.Profile,-10}  {run.Result,-10}  {run.EntryCount} entr(y/ies)  {kind}"));
            }

            return ExitCodes.Success;
        });

        return command;
    }
}
