namespace OpenTheWindows.Core.Abstractions;

/// <summary>When the remediation scheduled task runs.</summary>
public enum TaskTrigger
{
    /// <summary>Once a day.</summary>
    Daily = 0,

    /// <summary>Once a week.</summary>
    Weekly = 1,

    /// <summary>At every user logon.</summary>
    Logon = 2,

    /// <summary>
    /// At system start, re-applying only when the OS build/UBR has changed since
    /// the last run — the mechanism for restoring settings after a feature update
    /// (a feature update reboots, and the task's <c>remediate --if-build-changed</c>
    /// no-ops when the build is unchanged).
    /// </summary>
    BuildChange = 3,
}
