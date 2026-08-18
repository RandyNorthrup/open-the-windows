namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Registers and removes a scheduled task. The only task-writing capability the
/// product exposes; it never touches tasks outside its own
/// <c>\OpenTheWindows\</c> folder.
/// </summary>
public interface IScheduledTaskInstaller
{
    /// <summary>Registers (creating or replacing) the task described by <paramref name="spec"/>.</summary>
    void Install(ScheduledTaskSpec spec);

    /// <summary>Removes the task at <paramref name="taskPath"/>. Returns <see langword="false"/> if it did not exist.</summary>
    bool Remove(string taskPath);
}
