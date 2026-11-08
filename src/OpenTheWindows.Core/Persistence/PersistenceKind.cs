namespace OpenTheWindows.Core.Persistence;

/// <summary>The kinds of autostart the persistence watch records.</summary>
public enum PersistenceKind
{
    /// <summary>A Windows service (name, binary path, start type).</summary>
    Service = 0,

    /// <summary>A scheduled task (path, enabled state, actions).</summary>
    ScheduledTask = 1,

    /// <summary>A value under a Run or RunOnce key (machine-wide, 32-bit, or the signed-in user's).</summary>
    RunKey = 2,

    /// <summary>A file in the all-users or the signed-in user's Startup folder.</summary>
    StartupFolder = 3,
}
