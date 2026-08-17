using System.CommandLine;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

public sealed class ScanCommandTests
{
    private static (RootCommand Root, StringWriter Out, StringWriter Err) Build(bool supported = true)
    {
        OperatingSystemFacts os = supported
            ? FakeOperatingSystemInfo.Windows11Pro24H2()
            : FakeOperatingSystemInfo.WindowsServer2025();
        var doctor = new DoctorService(new FakeOperatingSystemInfo(os), new FakeElevationContext(isElevated: false));
        return CliTestHost.Host(TestCli.Services(doctor, _ => CatalogLoader.LoadBuiltIn()));
    }

    [Fact]
    public void Unsupported_platform_exits_3()
    {
        var (root, stdout, stderr) = Build(supported: false);

        int code = CliTestHost.Invoke(root, stdout, stderr, "scan", "--profile", "balanced");

        Assert.Equal(ExitCodes.UnsupportedPlatform, code);
        Assert.Contains("Unsupported", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_profile_exits_2()
    {
        var (root, stdout, stderr) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "scan", "--profile", "nope");

        Assert.Equal(ExitCodes.Error, code);
        Assert.Contains("Unknown profile", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Drift_exits_1_and_lists_ids()
    {
        var (root, stdout, stderr) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "scan", "--profile", "balanced", "--include-draft");

        Assert.Equal(ExitCodes.Drift, code);
        Assert.Contains("DRIFT", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Two_formats_exits_2()
    {
        var (root, stdout, stderr) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "scan", "--profile", "basic", "--json", "--csv");

        Assert.Equal(ExitCodes.Error, code);
    }

    [Fact]
    public void Json_format_writes_report_to_stdout()
    {
        var (root, stdout, stderr) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "scan", "--profile", "balanced", "--include-draft", "--json");

        Assert.Equal(ExitCodes.Drift, code);
        using var document = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("balanced", document.RootElement.GetProperty("profile").GetString());
    }

    [Fact]
    public void Out_file_writes_csv_report()
    {
        var (root, stdout, stderr) = Build();
        string path = Path.Combine(Path.GetTempPath(), "otw-scan-" + Guid.NewGuid().ToString("N") + ".csv");
        try
        {
            int code = CliTestHost.Invoke(
                root, stdout, stderr, "scan", "--profile", "balanced", "--include-draft", "--csv", "--out", path);

            Assert.Equal(ExitCodes.Drift, code);
            Assert.True(File.Exists(path));
            Assert.Contains("id,state,risk", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.Contains("Wrote csv report", stdout.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
