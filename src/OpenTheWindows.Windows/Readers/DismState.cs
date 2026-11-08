namespace OpenTheWindows.Windows.Readers;

/// <summary>
/// Parses the <c>State :</c> line of <c>dism /English /Get-FeatureInfo</c> or
/// <c>/Get-CapabilityInfo</c> output. A pending change counts as the state it is
/// heading to, so a change that only completes at the next reboot verifies.
/// </summary>
internal static class DismState
{
    /// <summary>The marker that identifies a Windows capability (Feature on Demand) rather than an optional feature.</summary>
    public const string CapabilityMarker = "~~~~";

    private const string StateLabel = "State";

    private static readonly Dictionary<string, bool> States = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Enabled"] = true,
        ["Enable Pending"] = true,
        ["Installed"] = true,
        ["Install Pending"] = true,
        ["Disabled"] = false,
        ["Disable Pending"] = false,
        ["Disabled with Payload Removed"] = false,
        ["Not Present"] = false,
        ["Staged"] = false,
        ["Uninstall Pending"] = false,
    };

    /// <summary>Whether <paramref name="name"/> is a capability (its identity carries <see cref="CapabilityMarker"/>).</summary>
    public static bool IsCapability(string name) => name.Contains(CapabilityMarker, StringComparison.Ordinal);

    /// <summary>The enabled/installed state the output reports, or <see langword="null"/> when it reports none.</summary>
    public static bool? Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        foreach (string line in output.Split('\n'))
        {
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0 && string.Equals(line[..colon].Trim(), StateLabel, StringComparison.OrdinalIgnoreCase))
            {
                return States.TryGetValue(line[(colon + 1)..].Trim(), out bool enabled) ? enabled : null;
            }
        }

        return null;
    }
}
