namespace OpenTheWindows.Core.Abstractions;

/// <summary>Truthful outcome of a System Restore point request.</summary>
public enum RestorePointStatus
{
    /// <summary>The caller did not ask for a restore point.</summary>
    NotRequested = 0,

    /// <summary>A restore point was created and its existence was verified.</summary>
    Created = 1,

    /// <summary>Windows declined because one was created within the frequency window (default 24 h) and force was not requested.</summary>
    Skipped24h = 2,

    /// <summary>System Restore is turned off for the system drive; nothing was created.</summary>
    Disabled = 3,

    /// <summary>The request failed for another reason (detail carries the cause).</summary>
    Failed = 4,
}
