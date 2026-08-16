using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Core.Diagnostics;

/// <summary>
/// Result of <see cref="DoctorService.Run"/>: OS facts, elevation, and the
/// support verdict with human-readable notes. Serialised as-is by the CLI.
/// </summary>
/// <param name="AppVersion">Version of the running product.</param>
/// <param name="OperatingSystem">OS facts.</param>
/// <param name="IsElevated">Whether the process is elevated.</param>
/// <param name="ProcessUserName">Account the process runs as.</param>
/// <param name="Status">Support verdict.</param>
/// <param name="Notes">Human-readable findings, one per line, never empty when status is not Supported.</param>
public sealed record SystemReport(
    string AppVersion,
    OperatingSystemFacts OperatingSystem,
    bool IsElevated,
    string ProcessUserName,
    SupportStatus Status,
    IReadOnlyList<string> Notes)
{
    /// <summary><see langword="true"/> when the engine may apply changes on this machine.</summary>
    public bool CanApply => Status == SupportStatus.Supported && IsElevated;
}
