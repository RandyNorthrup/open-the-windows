namespace OpenTheWindows.Core.Persistence;

/// <summary>How an autostart changed since the baseline.</summary>
public enum PersistenceChangeKind
{
    /// <summary>It is new since the baseline.</summary>
    Added = 0,

    /// <summary>It was in the baseline and is gone.</summary>
    Removed = 1,

    /// <summary>It is still there but runs something different or starts differently.</summary>
    Changed = 2,
}
