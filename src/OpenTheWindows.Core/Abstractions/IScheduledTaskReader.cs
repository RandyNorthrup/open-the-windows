namespace OpenTheWindows.Core.Abstractions;

/// <summary>Reads a scheduled task's enabled state. Never writes.</summary>
public interface IScheduledTaskReader
{
    /// <summary>Reads the task, or returns <see langword="null"/> when it does not exist.</summary>
    ScheduledTaskSnapshot? Read(string path);
}
