namespace OpenTheWindows.Core.Engine;

/// <summary>What an apply plan intends to do with one entry.</summary>
public enum PlanDisposition
{
    /// <summary>Apply the entry (it is applicable, permitted and in drift).</summary>
    Apply = 0,

    /// <summary>Already compliant; nothing to do.</summary>
    AlreadyCompliant = 1,

    /// <summary>Managed by MDM/Group Policy and break-glass was not requested; skipped.</summary>
    Managed = 2,

    /// <summary>Managed, but applied anyway under break-glass (audited).</summary>
    ManagedBreakGlass = 3,

    /// <summary>Does not apply to this machine (edition/build/architecture or absent target).</summary>
    NotApplicable = 4,

    /// <summary>Its risk tier is not permitted by the run's options; skipped.</summary>
    Blocked = 5,

    /// <summary>Contains an action a code-level safety rule refuses; never applied.</summary>
    Refused = 6,

    /// <summary>Conflicts with another entry selected in the same run; the run is rejected.</summary>
    Conflict = 7,
}
