namespace OpenTheWindows.Core.Audit;

/// <summary>The audit outcome of one baseline rule.</summary>
public enum AuditOutcome
{
    /// <summary>The control is satisfied.</summary>
    Pass,

    /// <summary>The control is not satisfied (drift, or a failing/warning health check).</summary>
    Fail,

    /// <summary>The control does not apply to this machine (edition, build or hardware).</summary>
    NotApplicable,

    /// <summary>The control needs a human decision: a setting managed by policy, or a manual-only rule.</summary>
    Manual,

    /// <summary>The control could not be determined (needs elevation, or is not detectable).</summary>
    Unknown,
}
