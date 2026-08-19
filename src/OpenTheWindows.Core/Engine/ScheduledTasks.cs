using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Core.Engine;

/// <summary>Shared shape of Open the Windows maintenance tasks: SYSTEM, highest privileges, hidden.</summary>
internal static class ScheduledTasks
{
    /// <summary>Builds a SYSTEM / highest-privileges / hidden task spec for the given command line and trigger.</summary>
    public static ScheduledTaskSpec System(string taskPath, string description, string executablePath, string arguments, TaskTrigger trigger)
        => new(taskPath, description, executablePath, arguments, trigger, RunAsSystem: true, HighestPrivileges: true, Hidden: true);
}
