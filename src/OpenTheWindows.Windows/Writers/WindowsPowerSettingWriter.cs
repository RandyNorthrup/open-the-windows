using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows.Writers;

/// <summary>
/// Sets a power setting's AC and DC value indices on the active scheme through
/// <c>powrprof.dll</c> and re-activates the scheme so the change takes effect.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsPowerSettingWriter : IPowerSettingWriter
{
    private const uint ErrorSuccess = 0;

    /// <inheritdoc />
    public void SetValues(Guid subgroup, Guid setting, uint ac, uint dc)
    {
        nint activeScheme = nint.Zero;
        try
        {
            if (PowerGetActiveScheme(nint.Zero, out activeScheme) != ErrorSuccess || activeScheme == nint.Zero)
            {
                throw new InvalidOperationException("Could not read the active power scheme.");
            }

            Guid scheme = Marshal.PtrToStructure<Guid>(activeScheme);
            ThrowOnError(PowerWriteACValueIndex(nint.Zero, scheme, subgroup, setting, ac), "AC");
            ThrowOnError(PowerWriteDCValueIndex(nint.Zero, scheme, subgroup, setting, dc), "DC");

            if (PowerSetActiveScheme(nint.Zero, scheme) != ErrorSuccess)
            {
                throw new InvalidOperationException("Could not re-activate the power scheme after writing the setting.");
            }
        }
        finally
        {
            if (activeScheme != nint.Zero)
            {
                _ = LocalFree(activeScheme);
            }
        }
    }

    private static void ThrowOnError(uint status, string rail)
    {
        if (status != ErrorSuccess)
        {
            throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture, $"Writing the {rail} power value failed (status {status})."));
        }
    }

    [LibraryImport("powrprof.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial uint PowerGetActiveScheme(nint userRootPowerKey, out nint activePolicyGuid);

    [LibraryImport("powrprof.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial uint PowerWriteACValueIndex(
        nint rootPowerKey, in Guid schemeGuid, in Guid subGroupGuid, in Guid settingGuid, uint acValueIndex);

    [LibraryImport("powrprof.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial uint PowerWriteDCValueIndex(
        nint rootPowerKey, in Guid schemeGuid, in Guid subGroupGuid, in Guid settingGuid, uint dcValueIndex);

    [LibraryImport("powrprof.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial uint PowerSetActiveScheme(nint userRootPowerKey, in Guid schemeGuid);

    [LibraryImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint LocalFree(nint mem);
}
