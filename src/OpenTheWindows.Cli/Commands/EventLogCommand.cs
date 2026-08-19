using System.CommandLine;
using System.ComponentModel;
using System.Globalization;
using System.Security;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Cli.Commands;

/// <summary>
/// <c>otw eventlog install|uninstall|status</c>: register or remove the Open the
/// Windows Event Log source (log <c>OpenTheWindows</c>, source <c>OTW</c>) that audit
/// events are written under. The MSI runs <c>otw eventlog install</c> as a deferred
/// custom action so the source exists before the first run; administrators can also
/// run it directly. <c>install</c> and <c>uninstall</c> need elevation; <c>status</c>
/// is a read. Exit codes: 0 success, 2 error, 3 unsupported OS, 5 elevation required.
/// </summary>
internal static class EventLogCommand
{
    public static Command Create(CliServices services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var command = new Command("eventlog", "Register or remove the Open the Windows Event Log source.");
        command.Subcommands.Add(CreateInstall(services));
        command.Subcommands.Add(CreateUninstall(services));
        command.Subcommands.Add(CreateStatus(services));
        return command;
    }

    private static Command CreateInstall(CliServices services)
    {
        var command = new Command("install", "Register the Event Log source (idempotent; needs elevation).");
        command.SetAction(parseResult => ExecuteInstall(parseResult, services));
        return command;
    }

    private static Command CreateUninstall(CliServices services)
    {
        var command = new Command("uninstall", "Remove the Event Log source and its custom log (needs elevation).");
        command.SetAction(parseResult => ExecuteUninstall(parseResult, services));
        return command;
    }

    private static Command CreateStatus(CliServices services)
    {
        var command = new Command("status", "Report the Event Log source and whether it is registered.");
        command.SetAction(parseResult => ExecuteStatus(parseResult, services));
        return command;
    }

    private static int ExecuteInstall(ParseResult parseResult, CliServices services)
    {
        if (!CommandSupport.TryBeginElevated(services, parseResult, "Registering the Event Log source", out TextWriter stdout, out TextWriter stderr, out int exitCode))
        {
            return exitCode;
        }

        IEventLogInstaller installer = services.CreateEventLogInstaller();
        try
        {
            bool created = installer.Install();
            stdout.WriteLine(created
                ? string.Create(CultureInfo.InvariantCulture, $"Registered Event Log source '{installer.SourceName}' in log '{installer.LogName}'.")
                : string.Create(CultureInfo.InvariantCulture, $"Event Log source '{installer.SourceName}' is already registered."));
            return ExitCodes.Success;
        }
        catch (Exception ex) when (IsEventLogFailure(ex))
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Could not register the Event Log source: {ex.Message}"));
            return ExitCodes.Error;
        }
    }

    private static int ExecuteUninstall(ParseResult parseResult, CliServices services)
    {
        if (!CommandSupport.TryBeginElevated(services, parseResult, "Removing the Event Log source", out TextWriter stdout, out TextWriter stderr, out int exitCode))
        {
            return exitCode;
        }

        IEventLogInstaller installer = services.CreateEventLogInstaller();
        try
        {
            bool removed = installer.Uninstall();
            stdout.WriteLine(removed
                ? string.Create(CultureInfo.InvariantCulture, $"Removed Event Log source '{installer.SourceName}' and log '{installer.LogName}'.")
                : string.Create(CultureInfo.InvariantCulture, $"No Event Log source '{installer.SourceName}' to remove."));
            return ExitCodes.Success;
        }
        catch (Exception ex) when (IsEventLogFailure(ex))
        {
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Could not remove the Event Log source: {ex.Message}"));
            return ExitCodes.Error;
        }
    }

    private static int ExecuteStatus(ParseResult parseResult, CliServices services)
    {
        TextWriter stdout = parseResult.InvocationConfiguration.Output;
        TextWriter stderr = parseResult.InvocationConfiguration.Error;

        if (!CommandSupport.IsSupported(services.Doctor, stderr, out int exitCode))
        {
            return exitCode;
        }

        IEventLogInstaller installer = services.CreateEventLogInstaller();
        bool registered = installer.SourceExists();
        stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Event Log source '{installer.SourceName}' in log '{installer.LogName}': {(registered ? "registered" : "not registered")}."));
        return ExitCodes.Success;
    }

    private static bool IsEventLogFailure(Exception ex)
        => ex is SecurityException or InvalidOperationException or Win32Exception or UnauthorizedAccessException;
}
