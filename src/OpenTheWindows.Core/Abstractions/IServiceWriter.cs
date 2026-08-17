using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Reconfigures a Windows service. Implementations refuse services that run as a
/// protected process (PPL) or that appear on
/// <see cref="ProtectedServices"/>, throwing rather than corrupting servicing or
/// security infrastructure.
/// </summary>
public interface IServiceWriter
{
    /// <summary>Sets the service start type.</summary>
    void SetStartType(string name, ServiceStartType startType);

    /// <summary>Stops the service if it is running, waiting up to <paramref name="timeout"/>.</summary>
    void StopService(string name, TimeSpan timeout);
}
