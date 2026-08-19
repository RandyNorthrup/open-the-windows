using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Runs the safety pre-flight before an apply: elevation, supported OS, pending
/// reboot, servicing in progress, OOBE and Safe Mode. Read-only.
/// </summary>
public interface IPreflight
{
    /// <summary>
    /// Evaluates every pre-flight condition and reports them. <paramref name="scope"/>
    /// determines whether elevation is required: a <see cref="Scope.User"/> run writes
    /// only the calling user's own hive and needs no administrator token, whereas a
    /// machine or all-users run does.
    /// </summary>
    PreflightReport Run(Scope scope);
}
