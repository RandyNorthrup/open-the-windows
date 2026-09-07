namespace OpenTheWindows.Windows.Updates;

/// <summary>WUA search strings used by hide/unhide.</summary>
internal static class WindowsUpdateSearchCriteria
{
    /// <summary>Updates the agent currently skips.</summary>
    public const string Hidden = "IsHidden=1";

    /// <summary>Updates not yet installed (the usual hide targets).</summary>
    public const string NotInstalled = "IsInstalled=0";
}
