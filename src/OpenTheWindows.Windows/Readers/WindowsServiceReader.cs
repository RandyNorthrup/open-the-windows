using System.Globalization;
using System.Runtime.Versioning;
using System.ServiceProcess;
using Microsoft.Win32;
using OpenTheWindows.Core.Abstractions;
using CatalogStartType = OpenTheWindows.Core.Catalog.ServiceStartType;

namespace OpenTheWindows.Windows.Readers;

/// <summary>
/// Reads a Windows service's start type and run state through the Service
/// Control Manager (<see cref="ServiceController"/>). Read-only.
/// </summary>
/// <remarks>
/// <see cref="ServiceControllerStatus"/> reports Automatic without distinguishing
/// delayed auto-start, so the <c>DelayedAutostart</c> flag is read from the
/// service's registry key. Protected-process (PPL) detection needs
/// <c>QueryServiceConfig2(SERVICE_CONFIG_LAUNCH_PROTECTED)</c>; it is not part of
/// scan compliance and is added in M3, where it guards apply. Until then the
/// conservative value <see langword="false"/> is reported.
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
            return new ServiceSnapshot(name, startType, running, IsProtectedProcess: false);
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
