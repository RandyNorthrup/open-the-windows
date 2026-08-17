namespace OpenTheWindows.Core.Journal;

/// <summary>The overall outcome of an apply or revert run.</summary>
public enum RunState
{
    /// <summary>The run is underway; the journal has been opened but not completed.</summary>
    InProgress = 0,

    /// <summary>Every planned entry was applied (or legitimately skipped) and verified.</summary>
    Completed = 1,

    /// <summary>A failure occurred and every already-applied action of the run was undone.</summary>
    RolledBack = 2,

    /// <summary>The run failed and could not be fully rolled back; the journal is the recovery record.</summary>
    Failed = 3,
}
