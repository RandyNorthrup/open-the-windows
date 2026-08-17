using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Windows.Readers;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// Reads real services through the Service Control Manager (read-only, no
/// elevation needed). Uses services present and running on every Windows box.
/// </summary>
public sealed class WindowsServiceReaderTests
{
    [Fact]
    public void Reads_a_core_running_service()
    {
        var reader = new WindowsServiceReader();

        // RpcSs (Remote Procedure Call) is Automatic and always running.
        ServiceSnapshot? snapshot = reader.Read("RpcSs");

        Assert.NotNull(snapshot);
        Assert.Equal("RpcSs", snapshot.Name);
        Assert.True(snapshot.IsRunning);
        Assert.True(snapshot.StartType is ServiceStartType.Automatic or ServiceStartType.AutomaticDelayed);
    }

    [Fact]
    public void Reads_a_service_start_type()
    {
        var reader = new WindowsServiceReader();

        // EventLog is Automatic and always running on any Windows machine.
        ServiceSnapshot? snapshot = reader.Read("EventLog");

        Assert.NotNull(snapshot);
        Assert.True(snapshot.StartType is ServiceStartType.Automatic or ServiceStartType.AutomaticDelayed);
    }

    [Fact]
    public void Missing_service_is_null()
    {
        var reader = new WindowsServiceReader();

        ServiceSnapshot? snapshot = reader.Read("OtwNoSuchServiceName");

        Assert.Null(snapshot);
    }
}
