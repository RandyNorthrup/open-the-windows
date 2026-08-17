namespace OpenTheWindows.Core.Journal;

/// <summary>The outcome of a single journaled action or entry.</summary>
public enum ApplyState
{
    /// <summary>Recorded but not yet applied (the initial journaled state, written before any machine write).</summary>
    Pending = 0,

    /// <summary>Applied and verified against the desired state.</summary>
    Applied = 1,

    /// <summary>The writer threw or verification failed; the run rolls back.</summary>
    Failed = 2,

    /// <summary>Applied earlier in this run, then undone during rollback.</summary>
    RolledBack = 3,

    /// <summary>Already compliant, so nothing was written.</summary>
    Skipped = 4,

    /// <summary>Managed by MDM or Group Policy; not changed (unless break-glass).</summary>
    Managed = 5,

    /// <summary>Refused by a code-level safety rule (protected service, disallowed executable).</summary>
    Refused = 6,

    /// <summary>Does not apply to this machine (absent service/task/feature).</summary>
    NotApplicable = 7,
}
