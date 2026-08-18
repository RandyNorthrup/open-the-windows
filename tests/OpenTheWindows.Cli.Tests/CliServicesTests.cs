namespace OpenTheWindows.Cli.Tests;

/// <summary>
/// Smoke test for the production service composition: constructing the real
/// Windows adapters is read-only and safe on any machine (nothing is applied).
/// </summary>
public sealed class CliServicesTests
{
    [Fact]
    public void CreateDefault_wires_every_service()
    {
        var services = CliServices.CreateDefault();

        Assert.NotNull(services.Doctor);
        Assert.NotNull(services.Health);
        Assert.NotNull(services.Elevation);
        Assert.NotNull(services.CreateScanEngine());
        Assert.NotNull(services.CreateApplyEngine());
        Assert.NotNull(services.CreateTaskInstaller());
        Assert.NotNull(services.CreateUserHiveEnumerator());
    }
}
