using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Startup;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw startup list [--json]</c> and <c>otw startup enable|disable &lt;name&gt; [--location &lt;loc&gt;]
/// [--what-if] [--json]</c>: the apps Windows starts at sign-in, and the Task Manager / Settings
/// startup switch for one of them. A change is a journaled run (profile <c>startup-apps</c>) that
/// <c>otw revert</c> undoes; it takes effect at the next sign-in. Exit codes: 0 done, 2 failed,
/// 3 unsupported OS, 4 no such app (or ambiguous without <c>--location</c>), 5 not elevated.
/// </summary>
internal static class StartupCommand
{
    /// <summary>Builds the <c>startup</c> command group.</summary>
    public static Command Create(CliServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var command = new Command("startup", "List the apps Windows starts at sign-in, or turn one on or off (journaled, revertable).");
        command.Subcommands.Add(CreateList(services));
        command.Subcommands.Add(CreateToggle(services, enable: true));
        command.Subcommands.Add(CreateToggle(services, enable: false));
        return command;
    }

    private static Command CreateList(CliServices services)
    {
        var json = new Option<bool>("--json") { Description = "Emit the list as JSON on stdout (stable schema)." };
        var command = new Command("list", "List the startup apps with their on/off state (read-only).");
        command.Options.Add(json);
        command.SetAction(parseResult =>
        {
            TextWriter stdout = parseResult.InvocationConfiguration.Output;
            if (!CommandSupport.IsSupported(services.Doctor, parseResult.InvocationConfiguration.Error, out int exitCode))
            {
                return exitCode;
            }

            IReadOnlyList<StartupApp> apps = services.CreateStartupApps().List();
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonSerializer.Serialize(apps, CliJsonContext.Default.IReadOnlyListStartupApp));
                return ExitCodes.Success;
            }

            if (apps.Count == 0)
            {
                stdout.WriteLine("No startup apps found.");
            }

            foreach (StartupApp app in apps)
            {
                stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{(app.Enabled ? "on " : "off")}  {app.Location,-20}  {app.Name}  {app.Command}"));
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static Command CreateToggle(CliServices services, bool enable)
    {
        var name = new Argument<string>("name") { Description = "The startup app's name, as `otw startup list` shows it." };
        var location = new Option<StartupLocation?>("--location") { Description = "Which registration to change when the name appears in more than one place." };
        var whatIf = new Option<bool>("--what-if") { Description = "Show what would change; change nothing." };
        var json = new Option<bool>("--json") { Description = "Emit the run result as JSON on stdout (stable schema)." };
        var command = new Command(
            enable ? "enable" : "disable",
            enable ? "Let a startup app start at sign-in again." : "Stop a startup app starting at sign-in (it stays installed).");
        command.Arguments.Add(name);
        command.Options.Add(location);
        command.Options.Add(whatIf);
        command.Options.Add(json);
        command.SetAction(parseResult => ExecuteToggle(parseResult, services, enable, new ToggleOptions(name, location, whatIf, json)));
        return command;
    }

    private static int ExecuteToggle(ParseResult parseResult, CliServices services, bool enable, ToggleOptions options)
    {
        TextWriter stdout = parseResult.InvocationConfiguration.Output;
        TextWriter stderr = parseResult.InvocationConfiguration.Error;
        bool whatIf = parseResult.GetValue(options.WhatIf);
        if (!CommandSupport.IsSupported(services.Doctor, stderr, out int exitCode))
        {
            return exitCode;
        }

        if (!whatIf && !services.Elevation.IsElevated)
        {
            stderr.WriteLine("Changing a startup app needs an elevated token. Re-run from an elevated prompt, or add --what-if.");
            return ExitCodes.ElevationRequired;
        }

        StartupAppsService startup = services.CreateStartupApps();
        string requested = parseResult.GetValue(options.Name)!;
        StartupLocation? where = parseResult.GetValue(options.Location);
        List<StartupApp> matches = [.. startup.List().Where(a =>
            string.Equals(a.Name, requested, StringComparison.OrdinalIgnoreCase) && (where is null || a.Location == where))];
        if (matches.Count == 0)
        {
            string at = where is null ? string.Empty : " at " + where;
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"No startup app named '{requested}'{at}. See `otw startup list`."));
            return ExitCodes.InvalidInput;
        }

        if (matches.Count > 1)
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"'{requested}' is registered in {matches.Count} places ({string.Join(", ", matches.Select(m => m.Location))}); pass --location."));
            return ExitCodes.InvalidInput;
        }

        ApplyResult result = startup.SetEnabled(matches[0], enable, whatIf);
        if (parseResult.GetValue(options.Json))
        {
            stdout.WriteLine(JsonSerializer.Serialize(ApplyReporting.ToJson(result), CliJsonContext.Default.ApplyJsonReport));
        }
        else
        {
            ApplyReporting.WriteText(result, whatIf, stdout);
        }

        return ApplyReporting.ExitCode(result, whatIf);
    }

    private sealed record ToggleOptions(Argument<string> Name, Option<StartupLocation?> Location, Option<bool> WhatIf, Option<bool> Json);
}
