using System.Runtime.Versioning;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Windows.Readers;
using OpenTheWindows.Windows.Writers;

namespace OpenTheWindows.Windows;

/// <summary>
/// Wires a production read-only <see cref="ScanEngine"/> over the real Windows
/// state readers, the managed-setting detector, the OS info and the system clock.
/// Shared by the CLI's service composition and the GUI's audit coordinator so the
/// reader wiring lives in exactly one place.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public static class WindowsScanEngineFactory
{
    /// <summary>Builds the scan engine over the real Windows readers.</summary>
    public static ScanEngine Create()
        => new(
            new WindowsRegistryReader(),
            new WindowsServiceReader(),
            new WindowsScheduledTaskReader(),
            new WindowsAppxReader(),
            new WindowsOptionalFeatureReader(),
            new WindowsDefenderPreferenceReader(),
            new WindowsPowerSettingReader(),
            new WindowsSecurityPolicyManager(new WindowsCommandRunner()),
            new WindowsInteractiveUserResolver(),
            new WindowsManagedSettingDetector(),
            new WindowsOperatingSystemInfo(),
            TimeProvider.System);
}
