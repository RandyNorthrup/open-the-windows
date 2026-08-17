using System.CommandLine;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

public sealed class HistoryAndRevertCommandTests
{
    private static (RootCommand Root, StringWriter Out, StringWriter Err) Build(ApplyEngine engine)
        => CliTestHost.Host(TestCli.Services(
            new DoctorService(new FakeOperatingSystemInfo(FakeOperatingSystemInfo.Windows11Pro24H2()), new FakeElevationContext(isElevated: true)),
            _ => CatalogLoader.LoadBuiltIn(),
            createApplyEngine: () => engine,
            elevation: new FakeElevationContext(isElevated: true)));

    [Fact]
    public void History_on_a_fresh_engine_reports_no_runs()
    {
        var (root, stdout, stderr) = Build(FakeApplyEngine.Create(new FakeMachine()).Engine);

        int code = CliTestHost.Invoke(root, stdout, stderr, "history");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("No runs recorded", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Revert_with_a_bad_token_exits_4()
    {
        var (root, stdout, stderr) = Build(FakeApplyEngine.Create(new FakeMachine()).Engine);

        int code = CliTestHost.Invoke(root, stdout, stderr, "revert", "not-a-guid");

        Assert.Equal(ExitCodes.InvalidInput, code);
    }

    [Fact]
    public void Revert_last_with_no_history_exits_4()
    {
        var (root, stdout, stderr) = Build(FakeApplyEngine.Create(new FakeMachine()).Engine);

        int code = CliTestHost.Invoke(root, stdout, stderr, "revert", "last");

        Assert.Equal(ExitCodes.InvalidInput, code);
    }

    [Fact]
    public void Apply_then_history_then_revert_round_trips()
    {
        ApplyHarness harness = FakeApplyEngine.Create(new FakeMachine());
        ApplyEngine engine = harness.Engine;

        var (applyRoot, applyOut, applyErr) = Build(engine);
        int applyCode = CliTestHost.Invoke(applyRoot, applyOut, applyErr, "apply", "--profile", "basic", "--include-draft", "--yes", "--no-restore-point");
        Assert.True(applyCode is ExitCodes.Success or ExitCodes.RebootRequired);

        var (historyRoot, historyOut, historyErr) = Build(engine);
        int historyCode = CliTestHost.Invoke(historyRoot, historyOut, historyErr, "history", "--json");
        Assert.Equal(ExitCodes.Success, historyCode);
        Assert.StartsWith("[", historyOut.ToString().TrimStart(), StringComparison.Ordinal);

        var (previewRoot, previewOut, previewErr) = Build(engine);
        int previewCode = CliTestHost.Invoke(previewRoot, previewOut, previewErr, "revert", "last", "--what-if");
        Assert.Equal(ExitCodes.Success, previewCode);
        Assert.Contains("Would revert", previewOut.ToString(), StringComparison.Ordinal);

        var (revertRoot, revertOut, revertErr) = Build(engine);
        int revertCode = CliTestHost.Invoke(revertRoot, revertOut, revertErr, "revert", "last", "--json");
        Assert.Equal(ExitCodes.Success, revertCode);
        using var document = JsonDocument.Parse(revertOut.ToString());
        Assert.Equal("Completed", document.RootElement.GetProperty("state").GetString());
    }
}
