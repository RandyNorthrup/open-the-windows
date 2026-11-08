using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows.Interop;

/// <summary>
/// Whether an organisation manages this device: enrolled in an MDM service (Intune or
/// another provider) or joined to an Active Directory domain. On such a device a live
/// policy value this product did not write came from that organisation.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class DeviceManagement
{
    private const string EnrollmentsKey = @"SOFTWARE\Microsoft\Enrollments";
    private const string EnrollmentStateValue = "EnrollmentState";
    private const string ProviderIdValue = "ProviderID";
    private const string DiscoveryUrlValue = "DiscoveryServiceFullURL";
    private const int EnrolledState = 1;
    private const int NetSetupDomainName = 3;
    private const int Success = 0;

    /// <summary>The managing authority (<see cref="ManagedState.Mdm"/> first), or <see langword="null"/> for an unmanaged device.</summary>
    public static ManagedState? Detect()
    {
        if (IsMdmEnrolled())
        {
            return ManagedState.Mdm;
        }

        return IsDomainJoined() ? ManagedState.GroupPolicy : null;
    }

    private static bool IsMdmEnrolled()
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using RegistryKey? enrollments = hklm.OpenSubKey(EnrollmentsKey, writable: false);
        if (enrollments is null)
        {
            return false;
        }

        foreach (string name in enrollments.GetSubKeyNames())
        {
            using RegistryKey? enrollment = enrollments.OpenSubKey(name, writable: false);
            // Stock Windows ships enrolled "Deploy/Cloud/Local Authority" entries with a provider id
            // but no management server; only a real MDM enrollment records the server it found.
            if (enrollment?.GetValue(EnrollmentStateValue) is int state && state == EnrolledState
                && enrollment.GetValue(ProviderIdValue) is string provider && provider.Length > 0
                && enrollment.GetValue(DiscoveryUrlValue) is string discovery && discovery.Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDomainJoined()
    {
        if (NetGetJoinInformation(null, out nint name, out int status) != Success)
        {
            return false;
        }

        _ = NetApiBufferFree(name);
        return status == NetSetupDomainName;
    }

    [LibraryImport("netapi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int NetGetJoinInformation(string? server, out nint nameBuffer, out int joinStatus);

    [LibraryImport("netapi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int NetApiBufferFree(nint buffer);
}
