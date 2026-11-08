using OpenTheWindows.Core.Updates;
using OpenTheWindows.Windows.Updates;

namespace OpenTheWindows.Windows.Tests;

/// <summary>In-memory Windows Update Agent for hide-service tests.</summary>
internal sealed class FakeWindowsUpdateAgent : IWindowsUpdateAgent
{
    public List<DiscoveredUpdate> NotInstalled { get; } = [];

    public List<DiscoveredUpdate> Hidden { get; } = [];

    public IReadOnlyList<DiscoveredUpdate> Search(string criteria)
    {
        if (string.Equals(criteria, WindowsUpdateSearchCriteria.Hidden, StringComparison.Ordinal))
        {
            return Hidden;
        }

        if (string.Equals(criteria, WindowsUpdateSearchCriteria.NotInstalled, StringComparison.Ordinal))
        {
            return NotInstalled;
        }

        return [];
    }

    /// <summary>When set, <see cref="SetHidden"/> is accepted but ignored (an agent that does not apply the change).</summary>
    public bool IgnoreChanges { get; set; }

    public void SetHidden(string identity, bool hidden)
    {
        if (IgnoreChanges)
        {
            return;
        }

        DiscoveredUpdate? match = NotInstalled.Concat(Hidden).FirstOrDefault(u => string.Equals(u.Identity, identity, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return;
        }

        var updated = match with { IsHidden = hidden };
        NotInstalled.RemoveAll(u => string.Equals(u.Identity, identity, StringComparison.OrdinalIgnoreCase));
        Hidden.RemoveAll(u => string.Equals(u.Identity, identity, StringComparison.OrdinalIgnoreCase));
        if (hidden)
        {
            Hidden.Add(updated);
        }
        else
        {
            NotInstalled.Add(updated);
        }
    }
}
