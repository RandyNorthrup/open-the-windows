using System.CommandLine;
using System.Text.Json;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Core.Updates;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

public sealed class UpdatesCommandTests
{
    private static DoctorService Doctor(bool supported = true)
        => new(
            new FakeOperatingSystemInfo(supported ? FakeOperatingSystemInfo.Windows11Pro24H2() : FakeOperatingSystemInfo.WindowsServer2025()),
            new FakeElevationContext(isElevated: true));

    private static (RootCommand Root, StringWriter Out, StringWriter Err, FakeMachine Machine) Build(
        bool supported = true, bool elevated = true, OperatingSystemFacts? os = null, DateTimeOffset? at = null,
        Action<FakeMachine>? seed = null)
    {
        var machine = new FakeMachine();
        seed?.Invoke(machine);
        (UpdateControlService service, _) = FakeUpdateControl.Create(machine, os, at);
        (RootCommand root, StringWriter stdout, StringWriter stderr) = CliTestHost.Host(TestCli.Services(
            Doctor(supported),
            _ => CatalogLoader.LoadBuiltIn(),
            createUpdateControl: () => service,
            elevation: new FakeElevationContext(elevated)));
        return (root, stdout, stderr, machine);
    }

    [Fact]
    public void Pause_on_unsupported_platform_exits_3()
    {
        var (root, stdout, stderr, _) = Build(supported: false);

        int code = CliTestHost.Invoke(root, stdout, stderr, "updates", "pause", "--days", "7");

        Assert.Equal(ExitCodes.UnsupportedPlatform, code);
    }

    [Fact]
    public void Pause_unelevated_exits_5()
    {
        var (root, stdout, stderr, _) = Build(elevated: false);

        int code = CliTestHost.Invoke(root, stdout, stderr, "updates", "pause", "--days", "7");

        Assert.Equal(ExitCodes.ElevationRequired, code);
    }

    [Fact]
    public void Pause_writes_the_values_and_reports_paused()
    {
        var (root, stdout, stderr, machine) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "updates", "pause", "--days", "7");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("Paused", stdout.ToString(), StringComparison.Ordinal);
        RegistryValueSnapshot snapshot = ((IRegistryReader)machine).Read(
            RegistryHive.LocalMachine, null, WindowsUpdateSettings.UxSettingsPath, WindowsUpdateSettings.PauseUpdatesExpiryTime);
        Assert.True(snapshot.Exists);
    }

    [Fact]
    public void Pause_beyond_cap_without_extended_exits_4()
    {
        var (root, stdout, stderr, _) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "updates", "pause", "--days", "90");

        Assert.Equal(ExitCodes.InvalidInput, code);
    }

    [Fact]
    public void Extended_pause_warns_and_succeeds()
    {
        var (root, stdout, stderr, _) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "updates", "pause", "--days", "90", "--extended");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("WARNING", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Resume_after_pause_reports_resumed_and_scans()
    {
        var (root, stdout, stderr, machine) = Build();
        Assert.Equal(ExitCodes.Success, CliTestHost.Invoke(root, stdout, stderr, "updates", "pause", "--days", "7"));

        int code = CliTestHost.Invoke(root, stdout, stderr, "updates", "resume");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("Resumed", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains(machine.Commands, c => string.Equals(c.Executable, UpdateControlService.ScanExecutable, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Resume_when_not_paused_reports_nothing_to_clear()
    {
        var (root, stdout, stderr, _) = Build();

        int code = CliTestHost.Invoke(root, stdout, stderr, "updates", "resume");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("not paused", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Status_reports_version_and_warning_without_elevation()
    {
        var (root, stdout, stderr, _) = Build(elevated: false);

        int code = CliTestHost.Invoke(root, stdout, stderr, "updates", "status");

        Assert.Equal(ExitCodes.Success, code);
        string text = stdout.ToString();
        Assert.Contains("24H2", text, StringComparison.Ordinal);
        Assert.Contains("WARNING", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_reports_a_target_release_pin()
    {
        var (root, stdout, stderr, _) = Build(seed: m => m.SetRegistry(
            RegistryHive.LocalMachine, null, WindowsUpdateSettings.PolicyPath,
            WindowsUpdateSettings.TargetReleaseVersionInfo, RegistryValueType.Sz, JsonSerializer.SerializeToElement("24H2")));

        int code = CliTestHost.Invoke(root, stdout, stderr, "updates", "status");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("Pinned to release 24H2", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Status_reports_unknown_end_of_service_for_an_uncatalogued_version()
    {
        var os = new OperatingSystemFacts("Windows 11 Pro", "Professional", "22H2", 22621, 1, "x64", "Client");
        var (root, stdout, stderr, _) = Build(os: os);

        int code = CliTestHost.Invoke(root, stdout, stderr, "updates", "status");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("unknown", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Status_reports_a_release_past_end_of_service()
    {
        var (root, stdout, stderr, _) = Build(at: new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero));

        int code = CliTestHost.Invoke(root, stdout, stderr, "updates", "status");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("ago", stdout.ToString(), StringComparison.Ordinal);
    }
}
