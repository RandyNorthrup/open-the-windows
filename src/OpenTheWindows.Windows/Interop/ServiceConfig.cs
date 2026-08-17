using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace OpenTheWindows.Windows.Interop;

/// <summary>
/// Thin Service Control Manager interop shared by the service reader (protected-
/// process query) and the service writer (start-type change). Handles are opened
/// with the least access needed and always closed.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class ServiceConfig
{
    private const uint ScManagerConnect = 0x0001;
    private const uint ServiceQueryConfig = 0x0001;
    private const uint ServiceChangeConfig = 0x0002;
    private const uint ServiceConfigDelayedAutoStartInfo = 3;
    private const uint ServiceConfigLaunchProtected = 12;
    private const uint ServiceNoChange = 0xFFFFFFFF;
    private const int LaunchProtectedInfoSize = 4;

    /// <summary>Win32 service start-type values for <see cref="SetStartType"/>.</summary>
    public const uint AutoStart = 2;
    public const uint DemandStart = 3;
    public const uint Disabled = 4;

    /// <summary>Returns whether the service runs as a protected process (PPL); read-only, needs no elevation.</summary>
    public static bool IsLaunchProtected(string serviceName)
    {
        nint scm = OpenSCManagerW(null, null, ScManagerConnect);
        if (scm == nint.Zero)
        {
            return false;
        }

        try
        {
            nint service = OpenServiceW(scm, serviceName, ServiceQueryConfig);
            if (service == nint.Zero)
            {
                return false;
            }

            try
            {
                byte[] buffer = new byte[LaunchProtectedInfoSize];
                if (!QueryServiceConfig2W(service, ServiceConfigLaunchProtected, buffer, (uint)buffer.Length, out _))
                {
                    return false;
                }

                return BitConverter.ToUInt32(buffer) != 0;
            }
            finally
            {
                _ = CloseServiceHandle(service);
            }
        }
        finally
        {
            _ = CloseServiceHandle(scm);
        }
    }

    /// <summary>Sets the service start type (and the delayed-auto-start flag) through <c>ChangeServiceConfig</c>.</summary>
    public static void SetStartType(string serviceName, uint startType, bool delayedAutoStart)
    {
        nint scm = OpenSCManagerW(null, null, ScManagerConnect);
        if (scm == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not open the Service Control Manager.");
        }

        try
        {
            nint service = OpenServiceW(scm, serviceName, ServiceChangeConfig | ServiceQueryConfig);
            if (service == nint.Zero)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(),
                    string.Create(CultureInfo.InvariantCulture, $"Could not open service '{serviceName}'."));
            }

            try
            {
                if (!ChangeServiceConfigW(service, ServiceNoChange, startType, ServiceNoChange,
                    null, null, nint.Zero, null, null, null, null))
                {
                    throw new Win32Exception(Marshal.GetLastPInvokeError(),
                        string.Create(CultureInfo.InvariantCulture, $"Could not change the start type of '{serviceName}'."));
                }

                if (startType == AutoStart)
                {
                    var info = new ServiceDelayedAutoStartInfo { DelayedAutostart = delayedAutoStart ? 1 : 0 };
                    if (!ChangeServiceConfig2W(service, ServiceConfigDelayedAutoStartInfo, in info))
                    {
                        throw new Win32Exception(Marshal.GetLastPInvokeError(),
                            string.Create(CultureInfo.InvariantCulture, $"Could not set the delayed-start flag of '{serviceName}'."));
                    }
                }
            }
            finally
            {
                _ = CloseServiceHandle(service);
            }
        }
        finally
        {
            _ = CloseServiceHandle(scm);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceDelayedAutoStartInfo
    {
        public int DelayedAutostart;
    }

    [LibraryImport("advapi32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint OpenSCManagerW(string? machineName, string? databaseName, uint desiredAccess);

    [LibraryImport("advapi32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial nint OpenServiceW(nint scManager, string serviceName, uint desiredAccess);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseServiceHandle(nint handle);

    [LibraryImport("advapi32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ChangeServiceConfigW(
        nint service, uint serviceType, uint startType, uint errorControl,
        string? binaryPathName, string? loadOrderGroup, nint tagId,
        string? dependencies, string? serviceStartName, string? password, string? displayName);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ChangeServiceConfig2W(nint service, uint infoLevel, in ServiceDelayedAutoStartInfo info);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryServiceConfig2W(nint service, uint infoLevel, [Out] byte[] buffer, uint bufferSize, out uint bytesNeeded);
}
