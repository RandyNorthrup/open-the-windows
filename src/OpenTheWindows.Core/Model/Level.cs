namespace OpenTheWindows.Core.Model;

/// <summary>
/// How aggressive a profile is within a category. Each catalogue entry declares
/// the lowest level at which it is applied by default. Levels are ordered:
/// a higher level includes every tweak of the lower levels.
/// </summary>
public enum Level
{
    /// <summary>Nothing is changed. Useful to exclude a whole category.</summary>
    Off = 0,

    /// <summary>Zero functional loss. Safe for any machine, including managed ones.</summary>
    Basic = 1,

    /// <summary>Recommended default. Minor, well-understood trade-offs.</summary>
    Balanced = 2,

    /// <summary>Accepts noticeable functional loss for privacy/security gains.</summary>
    Strict = 3,

    /// <summary>Maximum. Expects the operator to understand every side effect.</summary>
    Paranoid = 4,
}
