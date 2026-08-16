namespace OpenTheWindows.Core.Catalog;

/// <summary>Lifecycle of a catalogue entry.</summary>
public enum TweakStatus
{
    /// <summary>Authored from documentation; not yet verified on a lab image. Not applied by built-in profiles.</summary>
    Draft = 0,

    /// <summary>Verified on at least one build listed in <c>verifiedOn</c>.</summary>
    Verified = 1,

    /// <summary>Kept for revert/history only; never applied.</summary>
    Deprecated = 2,
}
