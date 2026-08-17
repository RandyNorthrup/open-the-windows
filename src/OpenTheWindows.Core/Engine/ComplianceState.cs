namespace OpenTheWindows.Core.Engine;

/// <summary>
/// Compliance of an action or entry against the desired state. Ordered by
/// severity for worst-state aggregation: <see cref="Drift"/> is worst, then
/// <see cref="Managed"/>, <see cref="Unknown"/>, <see cref="NotApplicable"/>,
/// and finally <see cref="Compliant"/>.
/// </summary>
public enum ComplianceState
{
    /// <summary>The observed state already matches the desired state.</summary>
    Compliant = 0,

    /// <summary>The action/entry does not apply to this machine and was not read.</summary>
    NotApplicable = 1,

    /// <summary>The result could not be determined (e.g. a command action, or an unreadable API).</summary>
    Unknown = 2,

    /// <summary>The setting is managed by MDM or Group Policy; the engine will not change it.</summary>
    Managed = 3,

    /// <summary>The observed state differs from the desired state.</summary>
    Drift = 4,
}
