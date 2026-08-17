using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Abstractions;

/// <summary>Observed state of a Windows service.</summary>
/// <param name="Name">Service short name.</param>
/// <param name="StartType">Configured start type.</param>
/// <param name="IsRunning">Whether the service is currently running.</param>
/// <param name="IsProtectedProcess">Whether the service runs as a protected process (PPL), which the engine must never reconfigure.</param>
public sealed record ServiceSnapshot(string Name, ServiceStartType StartType, bool IsRunning, bool IsProtectedProcess);
