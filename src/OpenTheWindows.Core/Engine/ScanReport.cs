using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Core.Engine;

/// <summary>The outcome of a scan: the machine's compliance against a profile's entries.</summary>
/// <param name="At">When the scan ran (from <see cref="TimeProvider"/>).</param>
/// <param name="Os">The machine facts at scan time.</param>
/// <param name="ProfileName">The profile that was scanned.</param>
/// <param name="Results">Per-entry observations.</param>
public sealed record ScanReport(
    DateTimeOffset At,
    OperatingSystemFacts Os,
    string ProfileName,
    IReadOnlyList<TweakObservation> Results)
{
    /// <summary>Number of entries in drift.</summary>
    public int DriftCount => Results.Count(r => r.State == ComplianceState.Drift);

    /// <summary>Number of compliant entries.</summary>
    public int CompliantCount => Results.Count(r => r.State == ComplianceState.Compliant);

    /// <summary>Number of entries managed by an organisation.</summary>
    public int ManagedCount => Results.Count(r => r.State == ComplianceState.Managed);

    /// <summary><see langword="true"/> when no entry is in drift.</summary>
    public bool IsCompliant => DriftCount == 0;
}
