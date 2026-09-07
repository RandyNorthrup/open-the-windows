using OpenTheWindows.Core.Updates;

namespace OpenTheWindows.Windows.Updates;

/// <summary>The Windows Update Agent operations hide/unhide need. Tests supply a fake.</summary>
internal interface IWindowsUpdateAgent
{
    /// <summary>Runs a WUA search with the given criteria string.</summary>
    IReadOnlyList<DiscoveredUpdate> Search(string criteria);

    /// <summary>Sets the hidden flag on the update with <paramref name="identity"/>.</summary>
    void SetHidden(string identity, bool hidden);
}
