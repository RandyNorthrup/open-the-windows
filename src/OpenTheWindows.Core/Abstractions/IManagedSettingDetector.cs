using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Detects whether a registry-backed setting is managed by MDM or Group Policy,
/// so the engine reports "managed by your organisation" instead of drift and the
/// UI can explain why a change is not offered.
/// </summary>
public interface IManagedSettingDetector
{
    /// <summary>Returns the managed state of the value at <paramref name="path"/>/<paramref name="name"/>.</summary>
    ManagedState Detect(RegistryHive hive, string path, string name);
}
