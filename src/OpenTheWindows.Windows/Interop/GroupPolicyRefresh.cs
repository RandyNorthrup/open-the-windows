using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace OpenTheWindows.Windows.Interop;

/// <summary>Asks the Group Policy service to apply computer policy now (<c>RefreshPolicyEx</c>).</summary>
[SupportedOSPlatform("windows")]
internal static partial class GroupPolicyRefresh
{
    private const uint ForceReapply = 0x1;

    /// <summary>Starts a forced computer-policy refresh; the service applies it in the background.</summary>
    public static void Machine()
    {
        if (!RefreshPolicyEx(machine: true, ForceReapply))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    [LibraryImport("userenv.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RefreshPolicyEx([MarshalAs(UnmanagedType.Bool)] bool machine, uint options);
}
