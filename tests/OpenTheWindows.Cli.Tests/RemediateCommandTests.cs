using System.CommandLine;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

public sealed class RemediateCommandTests
{
    private static (RootCommand Root, StringWriter Out, StringWriter Err) Build(
        bool supported = true, bool elevated = true, ApplyEngine? engine = null)
        => TestCli.ApplyHost(supported, elevated, engine);

    [Fact]
    public void Unsupported_platform_exits_3()
    {
        var (root, stdout, stderr) = Build(supported: false);

        int code = CliTestHost.Invoke(root, stdout, stderr, "remediate", "--profile", "basic");

        Assert.Equal(ExitCodes.UnsupportedPlatform, code);
    }

    [Fact]
    public void Not_elevated_exits_5()
    {
        var (root, stdout, stderr) = Build(elevated: false);

        int code = CliTestHost.Invoke(root, stdout, stderr, "remediate", "--profile", "home");

        Assert.Equal(ExitCodes.ElevationRequired, code);
    }

    [Fact]
    public void Unknown_profile_exits_4()
    {
        var (root, stdout, stderr) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "remediate", "--profile", "nope");

        Assert.Equal(ExitCodes.InvalidInput, code);
    }

    [Fact]
    public void What_if_makes_no_changes()
    {
        var machine = new FakeMachine();
        ApplyHarness harness = FakeApplyEngine.Create(machine);
        var (root, stdout, stderr) = Build(engine: harness.Engine);

        int code = CliTestHost.Invoke(root, stdout, stderr, "remediate", "--profile", "home", "--include-draft", "--what-if");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Equal(0, machine.WriteCount);
    }

    [Fact]
    public void Applies_and_writes_report_to_out_creating_its_directory()
    {
        var (root, stdout, stderr) = Build(engine: FakeApplyEngine.Create(new FakeMachine()).Engine);
        string path = Path.Combine(Path.GetTempPath(), "otw-remediate-" + Guid.NewGuid().ToString("N"), "last-remediate.json");
        try
        {
            int code = CliTestHost.Invoke(
                root, stdout, stderr, "remediate", "--profile", "basic", "--include-draft", "--no-restore-point", "--out", path);

            Assert.True(code is ExitCodes.Success or ExitCodes.RebootRequired, "remediation should apply and succeed");
            Assert.True(File.Exists(path), "the report is written to --out, creating its directory");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal("Completed", document.RootElement.GetProperty("state").GetString());
            Assert.Contains("Wrote remediation report", stdout.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            string? dir = Path.GetDirectoryName(path);
            if (dir is not null && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void Json_report_goes_to_stdout_when_no_out()
    {
        var (root, stdout, stderr) = Build(engine: FakeApplyEngine.Create(new FakeMachine()).Engine);

        int code = CliTestHost.Invoke(root, stdout, stderr, "remediate", "--profile", "basic", "--include-draft", "--no-restore-point", "--json");

        Assert.True(code is ExitCodes.Success or ExitCodes.RebootRequired);
        using var document = JsonDocument.Parse(stdout.ToString());
        Assert.Equal("Completed", document.RootElement.GetProperty("state").GetString());
    }
}
