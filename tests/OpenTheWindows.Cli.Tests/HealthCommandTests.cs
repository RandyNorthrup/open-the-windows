using System.CommandLine;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

public sealed class HealthCommandTests
{
    private static readonly HealthCheckResult Sample =
        new("boot.secure-boot", "Secure Boot", HealthStatus.Pass, "Secure Boot is enabled.", null);

    private static (RootCommand Root, StringWriter Out, StringWriter Err) Build()
    {
        var doctor = new DoctorService(
            new FakeOperatingSystemInfo(FakeOperatingSystemInfo.Windows11Pro24H2()),
            new FakeElevationContext(isElevated: false));
        return CliTestHost.Host(TestCli.Services(
            doctor, _ => CatalogLoader.LoadBuiltIn(), health: new FakeHealthProbe([Sample])));
    }

    [Fact]
    public void Health_text_exits_0_and_prints_checks()
    {
        var (root, stdout, stderr) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "health");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("Secure Boot", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Health_json_exits_0_and_parses()
    {
        var (root, stdout, stderr) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "health", "--json");

        Assert.Equal(ExitCodes.Success, code);
        using var document = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("boot.secure-boot", document.RootElement[0].GetProperty("id").GetString());
    }
}
