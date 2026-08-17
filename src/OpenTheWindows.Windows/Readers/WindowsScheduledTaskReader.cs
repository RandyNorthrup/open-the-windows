using System.Runtime.Versioning;
using Microsoft.Win32.TaskScheduler;
using OpenTheWindows.Core.Abstractions;
using SchedTask = Microsoft.Win32.TaskScheduler.Task;

namespace OpenTheWindows.Windows.Readers;

/// <summary>
/// Reads a scheduled task's enabled state through the Task Scheduler 2.0 API
/// (<see cref="TaskService"/>). Read-only: it never registers, deletes, enables
/// or runs a task.
/// </summary>
/// <remarks>
/// <see cref="TaskService.GetTask(string)"/> resolves a full task path such as
/// <c>\Microsoft\Windows\Defrag\ScheduledDefrag</c> and returns <see langword="null"/>
/// when the task is not registered, which maps to a <see langword="null"/>
/// snapshot (the engine treats that as not-applicable). <see cref="SchedTask.Enabled"/>
/// reflects the task's own enabled flag, independent of whether a trigger is
/// currently due.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsScheduledTaskReader : IScheduledTaskReader
{
    /// <inheritdoc />
    public ScheduledTaskSnapshot? Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var service = new TaskService();
        using SchedTask? task = service.GetTask(path);
        return task is null ? null : new ScheduledTaskSnapshot(path, task.Enabled, Exists: true);
    }
}
