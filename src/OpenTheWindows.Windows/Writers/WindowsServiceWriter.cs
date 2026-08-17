using System.Globalization;
using System.Runtime.Versioning;
using System.ServiceProcess;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Windows.Interop;
using CatalogStartType = OpenTheWindows.Core.Catalog.ServiceStartType;

namespace OpenTheWindows.Windows.Writers;

/// <summary>
/// Reconfigures a Windows service through the Service Control Manager. Refuses
/// services on <see cref="ProtectedServices"/> or that run as a protected
/// process (PPL), throwing <see cref="RefusedOperationException"/> before any
/// change — the engine also refuses these at plan time, but this is the last-line
/// guard.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsServiceWriter : IServiceWriter
{
    /// <inheritdoc />
    public void SetStartType(string name, CatalogStartType startType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Guard(name);

        (uint win32Start, bool delayed) = Map(startType);
        ServiceConfig.SetStartType(name, win32Start, delayed);
    }

    /// <inheritdoc />
    public void StopService(string name, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Guard(name);

        using var controller = new ServiceController(name);
        if (controller.Status is ServiceControllerStatus.Stopped or ServiceControllerStatus.StopPending)
        {
            return;
        }

        controller.Stop();
        controller.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
    }

    private static void Guard(string name)
    {
        if (ProtectedServices.IsProtected(name) || ServiceConfig.IsLaunchProtected(name))
        {
            throw new RefusedOperationException(
                string.Create(CultureInfo.InvariantCulture, $"Service '{name}' is protected and must not be reconfigured."));
        }
    }

    private static (uint Win32Start, bool Delayed) Map(CatalogStartType startType) => startType switch
    {
        CatalogStartType.Automatic => (ServiceConfig.AutoStart, false),
        CatalogStartType.AutomaticDelayed => (ServiceConfig.AutoStart, true),
        CatalogStartType.Manual => (ServiceConfig.DemandStart, false),
        CatalogStartType.Disabled => (ServiceConfig.Disabled, false),
        _ => throw new ArgumentOutOfRangeException(nameof(startType), startType, "Unsupported service start type."),
    };
}
