using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.ServiceProcess;
using Microsoft.Win32;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows;

/// <summary>
/// Read-only pre-flight for an apply run: elevation, supported OS, pending
/// reboot, servicing in progress, out-of-box experience and Safe Mode. Elevation,
/// supported OS, pending reboot, OOBE and Safe Mode are blocking; an in-progress
/// servicing session is a non-blocking warning.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsPreflight : IPreflight
{
    private const int SmCleanBoot = 67;

    private readonly IElevationContext _elevation;
    private readonly IOperatingSystemInfo _os;

    /// <summary>Creates the pre-flight over the elevation context and OS info.</summary>
    public WindowsPreflight(IElevationContext elevation, IOperatingSystemInfo os)
    {
        _elevation = elevation ?? throw new ArgumentNullException(nameof(elevation));
        _os = os ?? throw new ArgumentNullException(nameof(os));
    }

    /// <inheritdoc />
    public PreflightReport Run()
    {
        List<PreflightIssue> issues = [];

        if (!_elevation.IsElevated)
        {
            issues.Add(new PreflightIssue(PreflightCheck.Elevation, Blocking: true, "The process does not hold an elevated administrator token."));
        }

        OperatingSystemFacts facts = _os.GetFacts();
        if (!facts.IsWindows11)
        {
            issues.Add(new PreflightIssue(PreflightCheck.SupportedOs, Blocking: true, $"Unsupported OS: {facts.ProductName} build {facts.FullBuild}."));
        }

        if (IsRebootPending())
        {
            issues.Add(new PreflightIssue(PreflightCheck.PendingReboot, Blocking: true, "A reboot is pending from earlier servicing; apply after rebooting."));
        }

        if (IsOobe())
        {
            issues.Add(new PreflightIssue(PreflightCheck.Oobe, Blocking: true, "Windows setup / OOBE is still in progress."));
        }

        if (IsSafeMode())
        {
            issues.Add(new PreflightIssue(PreflightCheck.SafeMode, Blocking: true, "The machine is running in Safe Mode."));
        }

        if (IsServicingInProgress())
        {
            issues.Add(new PreflightIssue(PreflightCheck.ServicingInProgress, Blocking: false, "Windows servicing may be in progress (TrustedInstaller is running)."));
        }

        return new PreflightReport(issues);
    }

    private static bool IsRebootPending()
        => KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending")
            || KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired")
            || ValueExists(@"SYSTEM\CurrentControlSet\Control\Session Manager", "PendingFileRenameOperations");

    private static bool IsOobe()
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\Setup", writable: false);
        return key?.GetValue("SystemSetupInProgress") is int flag && flag != 0;
    }

    private static bool IsSafeMode() => GetSystemMetrics(SmCleanBoot) != 0;

    private static bool IsServicingInProgress()
    {
        try
        {
            using var controller = new ServiceController("TrustedInstaller");
            return controller.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool KeyExists(string path)
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(path, writable: false);
        return key is not null;
    }

    private static bool ValueExists(string path, string name)
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(path, writable: false);
        return key?.GetValue(name) is not null;
    }

    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int GetSystemMetrics(int index);
}
