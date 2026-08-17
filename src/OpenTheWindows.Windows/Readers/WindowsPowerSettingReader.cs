using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows.Readers;

/// <summary>
/// Reads a power setting's AC and DC value indices for the active power scheme
/// through <c>powrprof.dll</c>. Read-only: only the <c>PowerRead*</c> query
/// functions are used, never <c>PowerWrite*</c> or <c>PowerSetActiveScheme</c>.
/// </summary>
/// <remarks>
/// The M2 spec suggested CsWin32; the few power functions are declared here with
/// the <see cref="LibraryImportAttribute"/> source generator instead, so there
/// is no build-time code generator dependency and the marshalling stays
/// AOT/trim-safe. <c>PowerGetActiveScheme</c> allocates the active scheme GUID,
/// which is released with <c>LocalFree</c>. A setting the scheme does not define
/// yields <see langword="null"/>.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsPowerSettingReader : IPowerSettingReader
{
    private const uint ErrorSuccess = 0;

    /// <inheritdoc />
    public (uint Ac, uint Dc)? Read(Guid subgroup, Guid setting)
    {
        nint activeScheme = nint.Zero;
        try
        {
            if (PowerGetActiveScheme(nint.Zero, out activeScheme) != ErrorSuccess || activeScheme == nint.Zero)
            {
                return null;
            }

            Guid scheme = Marshal.PtrToStructure<Guid>(activeScheme);
            if (PowerReadACValueIndex(nint.Zero, scheme, subgroup, setting, out uint ac) != ErrorSuccess)
            {
                return null;
            }

            if (PowerReadDCValueIndex(nint.Zero, scheme, subgroup, setting, out uint dc) != ErrorSuccess)
            {
                return null;
            }

            return (ac, dc);
        }
        finally
        {
            if (activeScheme != nint.Zero)
            {
                _ = LocalFree(activeScheme);
            }
        }
    }

    [LibraryImport("powrprof.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial uint PowerGetActiveScheme(nint userRootPowerKey, out nint activePolicyGuid);

    [LibraryImport("powrprof.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial uint PowerReadACValueIndex(
        nint rootPowerKey, in Guid schemeGuid, in Guid subGroupGuid, in Guid settingGuid, out uint acValueIndex);

    [LibraryImport("powrprof.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial uint PowerReadDCValueIndex(
        nint rootPowerKey, in Guid schemeGuid, in Guid subGroupGuid, in Guid settingGuid, out uint dcValueIndex);

    [LibraryImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint LocalFree(nint mem);
}
