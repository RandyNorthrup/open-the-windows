using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Persistence;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw watch snapshot [--json]</c> records every autostart (services and drivers, scheduled
/// tasks, Run/RunOnce values, Startup-folder files) as the baseline; <c>otw watch diff [--json]</c>
/// lists what was added, removed or changed since. Both run elevated so they see the same
/// set. Exit codes: 0 no changes (or snapshot taken), 1 changes found, 2 no baseline or
/// error, 3 unsupported OS, 5 not elevated.
/// </summary>
internal static class WatchCommand
{
    /// <summary>Builds the <c>watch</c> command group.</summary>
    public static Command Create(CliServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var command = new Command("watch", "Record the machine's autostarts as a baseline, then list what changed since (read-only).");
        command.Subcommands.Add(CreateSnapshot(services));
        command.Subcommands.Add(CreateDiff(services));
        return command;
    }

    private static Command CreateSnapshot(CliServices services)
    {
        var json = new Option<bool>("--json") { Description = "Emit the result as JSON on stdout (stable schema)." };
        var command = new Command("snapshot", "Record every autostart now as the baseline (replacing the previous one).");
        command.Options.Add(json);
        command.SetAction(parseResult =>
        {
            if (!CommandSupport.TryBeginElevated(services, parseResult, "Taking the autostart baseline", out TextWriter stdout, out _, out int exitCode))
            {
                return exitCode;
            }

            PersistenceSnapshot snapshot = services.CreatePersistenceWatch().TakeSnapshot();
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonSerializer.Serialize(new WatchSnapshotJson(snapshot.TakenAt, snapshot.Items.Count), CliJsonContext.Default.WatchSnapshotJson));
            }
            else
            {
                stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Recorded {snapshot.Items.Count} autostarts as the baseline."));
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static Command CreateDiff(CliServices services)
    {
        var json = new Option<bool>("--json") { Description = "Emit the changes as JSON on stdout (stable schema)." };
        var command = new Command("diff", "List the autostarts added, removed or changed since the baseline.");
        command.Options.Add(json);
        command.SetAction(parseResult =>
        {
            if (!CommandSupport.TryBeginElevated(services, parseResult, "Comparing autostarts", out TextWriter stdout, out TextWriter stderr, out int exitCode))
            {
                return exitCode;
            }

            PersistenceComparison? comparison = services.CreatePersistenceWatch().Compare();
            if (comparison is null)
            {
                stderr.WriteLine("No baseline yet. Run `otw watch snapshot` first.");
                return ExitCodes.Error;
            }

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonSerializer.Serialize(WatchDiffJson.From(comparison), CliJsonContext.Default.WatchDiffJson));
            }
            else
            {
                WriteText(comparison, stdout);
            }

            return comparison.Changes.Count == 0 ? ExitCodes.Success : ExitCodes.Drift;
        });
        return command;
    }

    private static void WriteText(PersistenceComparison comparison, TextWriter stdout)
    {
        stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Baseline of {comparison.Baseline.TakenAt.ToLocalTime():yyyy-MM-dd HH:mm}: {comparison.Changes.Count} change(s)."));
        foreach (PersistenceChange change in comparison.Changes)
        {
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{change.Change,-8} {change.Item.Kind,-14} {change.Item.Key}"));
            if (change.Before is { } before && change.Change == PersistenceChangeKind.Changed)
            {
                stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"           was: {before.Detail}"));
            }

            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"           {(change.After is null ? "was" : "now")}: {change.Item.Detail}"));
        }
    }
}
