using System.Runtime.Versioning;
using Microsoft.Win32.TaskScheduler;
using OpenTheWindows.Core.Abstractions;
using SchedTask = Microsoft.Win32.TaskScheduler.Task;

namespace OpenTheWindows.Windows.Writers;

/// <summary>
/// Registers and removes Open the Windows scheduled tasks through the Task
/// Scheduler 2.0 API. It only ever touches the single task named in the spec (or
/// passed to <see cref="Remove"/>), never any other task on the machine.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsScheduledTaskInstaller : IScheduledTaskInstaller
{
    /// <inheritdoc />
    public void Install(ScheduledTaskSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        using var scheduler = new TaskService();
        using TaskDefinition definition = scheduler.NewTask();
        definition.RegistrationInfo.Description = spec.Description;
        definition.Settings.Hidden = spec.Hidden;

        if (spec.RunAsSystem)
        {
            definition.Principal.UserId = "SYSTEM";
            definition.Principal.LogonType = TaskLogonType.ServiceAccount;
        }

        definition.Principal.RunLevel = spec.HighestPrivileges ? TaskRunLevel.Highest : TaskRunLevel.LUA;
        definition.Triggers.Add(CreateTrigger(spec.Trigger));
        definition.Actions.Add(new ExecAction(spec.Executable, spec.Arguments, null));

        // RegisterTaskDefinition creates the \OpenTheWindows subfolder as needed and
        // replaces an existing task of the same name.
        scheduler.RootFolder.RegisterTaskDefinition(spec.TaskPath.TrimStart('\\'), definition);
    }

    /// <inheritdoc />
    public bool Remove(string taskPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskPath);

        using var scheduler = new TaskService();
        using SchedTask? task = scheduler.GetTask(taskPath);
        if (task is null)
        {
            return false;
        }

        task.Folder.DeleteTask(task.Name, exceptionOnNotExists: false);
        return true;
    }

    private static Trigger CreateTrigger(TaskTrigger trigger) => trigger switch
    {
        TaskTrigger.Daily => new DailyTrigger(),
        TaskTrigger.Weekly => new WeeklyTrigger(),
        TaskTrigger.Logon => new LogonTrigger(),
        // A feature update reboots, so a boot trigger fires after one; the task's
        // remediate runs with --if-build-changed and no-ops when the build is unchanged.
        TaskTrigger.BuildChange => new BootTrigger(),
        _ => throw new ArgumentOutOfRangeException(nameof(trigger), trigger, "Unknown task trigger."),
    };
}
