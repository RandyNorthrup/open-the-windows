using System.Globalization;
using System.Runtime.Versioning;
using System.ServiceProcess;
using Microsoft.Win32;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Windows.Interop;
using CatalogStartType = OpenTheWindows.Core.Catalog.ServiceStartType;

namespace OpenTheWindows.Windows.Readers;

/// <summary>
/// Reads a Windows service's start type and run state through the Service
/// Control Manager (<see cref="ServiceController"/>). Read-only.
/// </summary>
/// <remarks>
/// <see cref="ServiceControllerStatus"/> reports Automatic without distinguishing
/// delayed auto-start, so the <c>DelayedAutostart</c> flag is read from the
/// service's registry key. Protected-process (PPL) status comes from
/// <c>QueryServiceConfig2(SERVICE_CONFIG_LAUNCH_PROTECTED)</c> via
/// <see cref="Interop.ServiceConfig.IsLaunchProtected"/>; it guards apply so a
/// protected service is never reconfigured.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsServiceReader : IServiceReader
{
    private const string ServicesKeyPath = @"SYSTEM\CurrentControlSet\Services";

    /// <inheritdoc />
    public ServiceSnapshot? Read(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        try
        {
            using var controller = new ServiceController(name);
            CatalogStartType startType = MapStartType(controller.StartType, name);
            bool running = controller.Status == ServiceControllerStatus.Running;
            return new ServiceSnapshot(name, startType, running, ServiceConfig.IsLaunchProtected(name));
        }
        catch (InvalidOperationException)
        {
            // ServiceController throws when the service does not exist.
            return null;
        }
    }

    private static CatalogStartType MapStartType(ServiceStartMode mode, string name) => mode switch
    {
        ServiceStartMode.Automatic => IsDelayedAutoStart(name) ? CatalogStartType.AutomaticDelayed : CatalogStartType.Automatic,
        ServiceStartMode.Manual => CatalogStartType.Manual,
        ServiceStartMode.Disabled => CatalogStartType.Disabled,
        // The remaining kernel-driver start modes are never targeted by the
        // catalogue and map to the closest Win32 start type for a stable report.
        _ => CatalogStartType.Automatic,
    };

    private static bool IsDelayedAutoStart(string name)
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            string.Create(CultureInfo.InvariantCulture, $@"{ServicesKeyPath}\{name}"), writable: false);
        return key?.GetValue("DelayedAutostart") is int flag && flag != 0;
    }
}
