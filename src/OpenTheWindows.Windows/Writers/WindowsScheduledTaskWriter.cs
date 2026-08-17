using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32.TaskScheduler;
using OpenTheWindows.Core.Abstractions;
using SchedTask = Microsoft.Win32.TaskScheduler.Task;

namespace OpenTheWindows.Windows.Writers;

/// <summary>Enables or disables a scheduled task through the Task Scheduler 2.0 API.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsScheduledTaskWriter : IScheduledTaskWriter
{
    /// <inheritdoc />
    public void SetEnabled(string path, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var scheduler = new TaskService();
        using SchedTask? task = scheduler.GetTask(path)
            ?? throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"Scheduled task '{path}' does not exist."));
        task.Enabled = enabled;
    }
}
