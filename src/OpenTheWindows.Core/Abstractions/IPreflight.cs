namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Runs the safety pre-flight before an apply: elevation, supported OS, pending
/// reboot, servicing in progress, OOBE and Safe Mode. Read-only.
/// </summary>
public interface IPreflight
{
    /// <summary>Evaluates every pre-flight condition and reports them.</summary>
    PreflightReport Run();
}
