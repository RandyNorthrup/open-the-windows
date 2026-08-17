namespace OpenTheWindows.Core.Abstractions;

/// <summary>Result of a single read-only machine health check.</summary>
public enum HealthStatus
{
    /// <summary>The check passed.</summary>
    Pass = 0,

    /// <summary>The check found a non-ideal but non-critical condition.</summary>
    Warn = 1,

    /// <summary>The check failed.</summary>
    Fail = 2,

    /// <summary>The check does not apply to this machine.</summary>
    NotApplicable = 3,

    /// <summary>The check could not determine a result (e.g. an API was unavailable).</summary>
    Unknown = 4,
}
