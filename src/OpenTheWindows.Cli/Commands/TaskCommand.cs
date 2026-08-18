using System.CommandLine;
using System.Globalization;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw task install --profile &lt;id&gt; [--daily|--weekly|--on-logon]</c> and
/// <c>otw task remove</c>: register or remove the drift-remediation scheduled task
/// (<c>\OpenTheWindows\Remediate</c>, SYSTEM, highest privileges, hidden) that runs
/// <c>otw remediate</c> for a profile. Both need elevation. Exit codes: 0 success,
/// 2 error, 3 unsupported OS, 4 invalid input, 5 elevation required.
/// </summary>
internal static class TaskCommand
{
    public static Command Create(CliServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var command = new Command("task", "Install or remove the drift-remediation scheduled task.");
        command.Subcommands.Add(CreateInstall(services));
        command.Subcommands.Add(CreateRemove(services));
        return command;
    }

    private static Command CreateInstall(CliServices services)
    {
        var profile = CommandSupport.ProfileOption();
        var daily = new Option<bool>("--daily") { Description = "Run once a day (the default schedule)." };
        var weekly = new Option<bool>("--weekly") { Description = "Run once a week." };
        var onLogon = new Option<bool>("--on-logon") { Description = "Run at every user logon." };

        var command = new Command("install", "Register the remediation task for a profile (replacing any existing one).");
        command.Options.Add(profile);
        command.Options.Add(daily);
        command.Options.Add(weekly);
        command.Options.Add(onLogon);
        command.SetAction(parseResult => ExecuteInstall(parseResult, services, profile, daily, weekly, onLogon));
        return command;
    }

    private static Command CreateRemove(CliServices services)
    {
        var command = new Command("remove", "Remove the remediation task.");
        command.SetAction(parseResult => ExecuteRemove(parseResult, services));
        return command;
    }

    private static int ExecuteInstall(
        ParseResult parseResult, CliServices services, Option<string> profileOption,
        Option<bool> dailyOption, Option<bool> weeklyOption, Option<bool> onLogonOption)
    {
        if (!TryBeginElevated(parseResult, services, "Installing", out TextWriter stdout, out TextWriter stderr, out int exitCode))
        {
            return exitCode;
        }

        string profile = parseResult.GetValue(profileOption) ?? string.Empty;
        if (!IsKnownProfile(profile))
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Unknown profile '{profile}'. Use a level ({string.Join(", ", NamedProfile.Names)}), a built-in profile id " +
                $"({string.Join(", ", ProfileLoader.BuiltInIds)}), or a path to a profile .json file."));
            return ExitCodes.InvalidInput;
        }

        if (!TryResolveTrigger(parseResult, dailyOption, weeklyOption, onLogonOption, out TaskTrigger trigger))
        {
            stderr.WriteLine("Choose at most one of --daily, --weekly, --on-logon.");
            return ExitCodes.InvalidInput;
        }

        string? executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable))
        {
            stderr.WriteLine("Could not determine this executable's path to schedule.");
            return ExitCodes.Error;
        }

        ScheduledTaskSpec spec = RemediationTask.Build(profile, trigger, executable, RemediationTask.DefaultReportPath);
        services.CreateTaskInstaller().Install(spec);
        stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Installed task '{spec.TaskPath}' ({trigger}) for profile '{profile}'. Reports: {RemediationTask.DefaultReportPath}"));
        return ExitCodes.Success;
    }

    private static int ExecuteRemove(ParseResult parseResult, CliServices services)
    {
        if (!TryBeginElevated(parseResult, services, "Removing", out TextWriter stdout, out _, out int exitCode))
        {
            return exitCode;
        }

        bool removed = services.CreateTaskInstaller().Remove(RemediationTask.TaskPath);
        stdout.WriteLine(removed
            ? string.Create(CultureInfo.InvariantCulture, $"Removed task '{RemediationTask.TaskPath}'.")
            : string.Create(CultureInfo.InvariantCulture, $"No task '{RemediationTask.TaskPath}' to remove."));
        return ExitCodes.Success;
    }

    private static bool TryBeginElevated(
        ParseResult parseResult, CliServices services, string action, out TextWriter stdout, out TextWriter stderr, out int exitCode)
    {
        stdout = parseResult.InvocationConfiguration.Output;
        stderr = parseResult.InvocationConfiguration.Error;

        if (!CommandSupport.IsSupported(services.Doctor, stderr, out exitCode))
        {
            return false;
        }

        if (!services.Elevation.IsElevated)
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{action} the remediation task needs an elevated token. Re-run from an elevated prompt."));
            exitCode = ExitCodes.ElevationRequired;
            return false;
        }

        return true;
    }

    private static bool TryResolveTrigger(
        ParseResult parseResult, Option<bool> dailyOption, Option<bool> weeklyOption, Option<bool> onLogonOption, out TaskTrigger trigger)
    {
        bool daily = parseResult.GetValue(dailyOption);
        bool weekly = parseResult.GetValue(weeklyOption);
        bool onLogon = parseResult.GetValue(onLogonOption);

        int chosen = (daily ? 1 : 0) + (weekly ? 1 : 0) + (onLogon ? 1 : 0);
        if (chosen > 1)
        {
            trigger = TaskTrigger.Daily;
            return false;
        }

        if (weekly)
        {
            trigger = TaskTrigger.Weekly;
        }
        else if (onLogon)
        {
            trigger = TaskTrigger.Logon;
        }
        else
        {
            trigger = TaskTrigger.Daily;
        }

        return true;
    }

    /// <summary>A profile is installable if it names a level, a built-in id, or an existing profile file.</summary>
    private static bool IsKnownProfile(string value)
        => !string.IsNullOrEmpty(value)
            && (NamedProfile.TryResolve(value, out _) || ProfileLoader.TryLoadBuiltIn(value) is not null || File.Exists(value));
}
