namespace OpenTheWindows.Core.Model;

/// <summary>
/// Per-tweak breakage risk, independent of <see cref="Level"/>. Drives UI
/// badges, confirmation prompts, and which tweaks unattended mode may apply
/// without an explicit allow-list.
/// </summary>
public enum RiskTier
{
    /// <summary>No known side effects; silently reversible.</summary>
    Safe = 0,

    /// <summary>Documented, minor side effects (a feature disappears, a UI changes).</summary>
    Caution = 1,

    /// <summary>Can break workflows or software; requires understanding of the setting.</summary>
    Advanced = 2,

    /// <summary>Known to break something important for some users; requires typed confirmation.</summary>
    Breaking = 3,
}
