using System.CommandLine;
using System.Globalization;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Audit;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Profiles;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw task install --profile &lt;id&gt; [--daily|--weekly|--on-logon|--on-build-change]</c>
/// and <c>otw task remove</c>: register or remove the drift-remediation scheduled task
/// (<c>\OpenTheWindows\Remediate</c>, SYSTEM, highest privileges, hidden) that runs
/// <c>otw remediate</c> for a profile. <c>--on-build-change</c> registers a boot-triggered
/// task that re-applies only after a feature update. Both need elevation. Exit codes:
/// 0 success, 2 error, 3 unsupported OS, 4 invalid input, 5 elevation required.
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
        var audit = new Option<string?>("--audit") { Description = "Install an audit task for this baseline id (see 'otw audit list') instead of a remediation task; requires --out." };
        var outOption = new Option<string?>("--out") { Description = "For an audit task: the path the JSON report is written to (for example a fleet UNC share)." };
        var daily = new Option<bool>("--daily") { Description = "Run once a day (the default schedule)." };
        var weekly = new Option<bool>("--weekly") { Description = "Run once a week." };
        var onLogon = new Option<bool>("--on-logon") { Description = "Run at every user logon." };
        var onBuildChange = new Option<bool>("--on-build-change") { Description = "Run at system start, re-applying only after a feature update (an OS build/UBR change)." };
        var whatIf = new Option<bool>("--what-if") { Description = "Show the task that would be registered (path, trigger, command line); change nothing." };

        var command = new Command("install", "Register a remediation task for a profile, or an audit task for a baseline (replacing any existing one).");
        command.Options.Add(profile);
        command.Options.Add(audit);
        command.Options.Add(outOption);
        command.Options.Add(daily);
        command.Options.Add(weekly);
        command.Options.Add(onLogon);
        command.Options.Add(onBuildChange);
        command.Options.Add(whatIf);
        command.SetAction(parseResult => ExecuteInstall(parseResult, services, profile, audit, outOption, new TriggerOptions(daily, weekly, onLogon, onBuildChange), whatIf));
        return command;
    }

    private static Command CreateRemove(CliServices services)
    {
        var command = new Command("remove", "Remove the remediation task and any per-user logon fallback components.");
        command.SetAction(parseResult => ExecuteRemove(parseResult, services));
        return command;
    }

    private static int ExecuteInstall(
        ParseResult parseResult, CliServices services, Option<string> profileOption, Option<string?> auditOption, Option<string?> outOption,
        TriggerOptions triggers, Option<bool> whatIfOption)
    {
        bool whatIf = parseResult.GetValue(whatIfOption);
        TextWriter stdout = parseResult.InvocationConfiguration.Output;
        TextWriter stderr = parseResult.InvocationConfiguration.Error;
        if (!(whatIf
            ? CommandSupport.IsSupported(services.Doctor, stderr, out int exitCode)
            : CommandSupport.TryBeginElevated(services, parseResult, "Installing the scheduled task", out stdout, out stderr, out exitCode)))
        {
            return exitCode;
        }

        string profile = parseResult.GetValue(profileOption) ?? string.Empty;
        string? auditBaseline = parseResult.GetValue(auditOption);
        if (auditBaseline is not null && profile.Length > 0)
        {
            stderr.WriteLine("Choose either --profile (a remediation task) or --audit (an audit task), not both.");
            return ExitCodes.InvalidInput;
        }

        if (!TryResolveTrigger(parseResult, triggers.Daily, triggers.Weekly, triggers.OnLogon, triggers.OnBuildChange, out TaskTrigger trigger))
        {
            stderr.WriteLine("Choose at most one of --daily, --weekly, --on-logon, --on-build-change.");
            return ExitCodes.InvalidInput;
        }

        string? executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable))
        {
            stderr.WriteLine("Could not determine this executable's path to schedule.");
            return ExitCodes.Error;
        }

        // The task runs this file as SYSTEM: refuse when a standard user could swap or side-load it.
        if (services.Hardening.ExecutableGuard.FindWeakness(executable) is { } weakness)
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Refusing to schedule a SYSTEM task: {weakness}"));
            return ExitCodes.Error;
        }

        var target = new TaskTarget(trigger, executable, whatIf, stdout, stderr);
        return auditBaseline is not null
            ? InstallAuditTask(services, auditBaseline, parseResult.GetValue(outOption), target)
            : InstallRemediationTask(services, profile, target);
    }

    private static int InstallRemediationTask(CliServices services, string profile, TaskTarget target)
    {
        (TaskTrigger trigger, string executable, _, TextWriter stdout, TextWriter stderr) = target;
        if (!IsKnownProfile(profile))
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Unknown profile '{profile}'. Use a level ({string.Join(", ", NamedProfile.Names)}), a built-in profile id " +
                $"({string.Join(", ", ProfileLoader.BuiltInIds)}), or a path to a profile .json file."));
            return ExitCodes.InvalidInput;
        }

        if (target.WhatIf)
        {
            ScheduledTaskSpec preview = RemediationTask.Build(profile, trigger, executable, RemediationTask.DefaultReportPath);
            return Preview(preview, File.Exists(profile) ? "the profile file is copied to the protected profiles folder first" : null, stdout);
        }

        string staged = ProfileStaging.Stage(profile, services.Hardening);
        ScheduledTaskSpec spec = RemediationTask.Build(staged, trigger, executable, RemediationTask.DefaultReportPath);
        services.CreateTaskInstaller().Install(spec);
        stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Installed task '{spec.TaskPath}' ({trigger}) for profile '{staged}'. Reports: {RemediationTask.DefaultReportPath}"));
        return ExitCodes.Success;
    }

    private static int InstallAuditTask(CliServices services, string baselineId, string? outPath, TaskTarget target)
    {
        (TaskTrigger trigger, string executable, _, TextWriter stdout, TextWriter stderr) = target;
        if (BaselineLoader.TryLoadBuiltIn(baselineId) is null)
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Unknown baseline '{baselineId}'. Use a built-in id ({string.Join(", ", BaselineLoader.BuiltInIds)})."));
            return ExitCodes.InvalidInput;
        }

        if (string.IsNullOrEmpty(outPath))
        {
            stderr.WriteLine("--audit requires --out (the path the report is written to, for example a fleet UNC share).");
            return ExitCodes.InvalidInput;
        }

        // A relative path would resolve against the task's working folder (System32) at run time.
        outPath = Path.GetFullPath(outPath);
        ScheduledTaskSpec spec = AuditReportTask.Build(baselineId, trigger, executable, outPath);
        if (target.WhatIf)
        {
            return Preview(spec, null, stdout);
        }

        services.CreateTaskInstaller().Install(spec);
        stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Installed task '{spec.TaskPath}' ({trigger}) auditing baseline '{baselineId}'. Report: {outPath}"));
        return ExitCodes.Success;
    }

    private static int Preview(ScheduledTaskSpec spec, string? note, TextWriter stdout)
    {
        stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Would install task '{spec.TaskPath}' ({spec.Trigger}, {(spec.RunAsSystem ? "as SYSTEM" : "as the user")}): \"{spec.Executable}\" {spec.Arguments}"));
        if (note is not null)
        {
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Before that, {note}."));
        }

        return ExitCodes.Success;
    }

    private static int ExecuteRemove(ParseResult parseResult, CliServices services)
    {
        if (!CommandSupport.TryBeginElevated(services, parseResult, "Removing the remediation task", out TextWriter stdout, out _, out int exitCode))
        {
            return exitCode;
        }

        bool removed = services.CreateTaskInstaller().Remove(RemediationTask.TaskPath);
        stdout.WriteLine(removed
            ? string.Create(CultureInfo.InvariantCulture, $"Removed task '{RemediationTask.TaskPath}'.")
            : string.Create(CultureInfo.InvariantCulture, $"No task '{RemediationTask.TaskPath}' to remove."));

        // The Active Setup logon fallback is the other half of a profile's re-enforcement
        // footprint; clearing the task without it would leave users re-enforced at sign-in.
        int fallbacks = services.CreateActiveSetupRegistrar().RemoveAll();
        if (fallbacks > 0)
        {
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Removed {fallbacks} per-user logon fallback component(s)."));
        }

        return ExitCodes.Success;
    }

    private static bool TryResolveTrigger(
        ParseResult parseResult, Option<bool> dailyOption, Option<bool> weeklyOption, Option<bool> onLogonOption,
        Option<bool> onBuildChangeOption, out TaskTrigger trigger)
    {
        bool daily = parseResult.GetValue(dailyOption);
        bool weekly = parseResult.GetValue(weeklyOption);
        bool onLogon = parseResult.GetValue(onLogonOption);
        bool onBuildChange = parseResult.GetValue(onBuildChangeOption);

        int chosen = (daily ? 1 : 0) + (weekly ? 1 : 0) + (onLogon ? 1 : 0) + (onBuildChange ? 1 : 0);
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
        else if (onBuildChange)
        {
            trigger = TaskTrigger.BuildChange;
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

    private sealed record TriggerOptions(Option<bool> Daily, Option<bool> Weekly, Option<bool> OnLogon, Option<bool> OnBuildChange);

    private sealed record TaskTarget(TaskTrigger Trigger, string Executable, bool WhatIf, TextWriter Stdout, TextWriter Stderr);
}
