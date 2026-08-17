namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Stable Windows Event Log / audit event identifiers. Values are a contract:
/// they never change once released and new ones are appended. See
/// <c>docs/event-log.md</c>.
/// </summary>
public enum AuditEventId
{
    /// <summary>Unused sentinel; never emitted. Present so the enum has a zero value.</summary>
    None = 0,

    /// <summary>An apply run started.</summary>
    ApplyStarted = 1000,

    /// <summary>One tweak was applied and verified.</summary>
    TweakApplied = 1001,

    /// <summary>One tweak failed verification or its writer threw.</summary>
    TweakFailed = 1002,

    /// <summary>An apply run was rolled back after a failure.</summary>
    RunRolledBack = 1003,

    /// <summary>A revert run started.</summary>
    RevertStarted = 1004,

    /// <summary>A revert run completed.</summary>
    RevertCompleted = 1005,

    /// <summary>A scan detected drift.</summary>
    ScanDrift = 2000,

    /// <summary>A single action was rolled back during a run's rollback.</summary>
    Rollback = 3000,

    /// <summary>A refused operation was attempted (protected service, disallowed executable, managed setting without break-glass).</summary>
    Refusal = 4000,

    /// <summary>A managed setting was overridden under break-glass.</summary>
    BreakGlass = 4001,

    /// <summary>An unexpected error.</summary>
    Error = 9000,
}
