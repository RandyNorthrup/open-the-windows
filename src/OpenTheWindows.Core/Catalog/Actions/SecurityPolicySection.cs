namespace OpenTheWindows.Core.Catalog.Actions;

/// <summary>
/// A section of the Windows security policy database — the <c>secedit</c> security
/// template areas. Account, password and lockout policy all live in the
/// <c>[System Access]</c> section; the enum is the typed, extensible boundary for
/// which sections a <see cref="SecurityPolicyAction"/> may target, so the closed
/// action set never grows an untyped free-text section name.
/// </summary>
public enum SecurityPolicySection
{
    /// <summary>
    /// The <c>[System Access]</c> section: password policy (length, complexity,
    /// history, age) and account lockout policy (threshold, duration, reset).
    /// </summary>
    SystemAccess,
}
