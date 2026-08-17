namespace OpenTheWindows.Core.Abstractions;

/// <summary>Enables or disables a scheduled task by its full path.</summary>
public interface IScheduledTaskWriter
{
    /// <summary>Sets the task's enabled state.</summary>
    void SetEnabled(string path, bool enabled);
}
