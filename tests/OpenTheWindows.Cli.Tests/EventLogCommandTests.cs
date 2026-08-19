using System.CommandLine;
using OpenTheWindows.Core;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

public sealed class EventLogCommandTests
{
    private static (RootCommand Root, StringWriter Out, StringWriter Err) Build(
        FakeEventLogInstaller installer, bool supported = true, bool elevated = true)
        => TestCli.ApplyHost(supported, elevated, eventLogInstaller: installer);

    [Fact]
    public void Install_registers_the_source()
    {
        var installer = new FakeEventLogInstaller { Registered = false };
        var (root, stdout, stderr) = Build(installer);

        int code = CliTestHost.Invoke(root, stdout, stderr, "eventlog", "install");

        Assert.Equal(ExitCodes.Success, code);
        Assert.True(installer.Registered);
        Assert.Equal(1, installer.InstallCalls);
        Assert.Contains("Registered Event Log source 'OTW'", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Install_is_idempotent_when_already_registered()
    {
        var installer = new FakeEventLogInstaller { Registered = true };
        var (root, stdout, stderr) = Build(installer);

        int code = CliTestHost.Invoke(root, stdout, stderr, "eventlog", "install");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("already registered", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Install_not_elevated_exits_5()
    {
        var installer = new FakeEventLogInstaller();
        var (root, stdout, stderr) = Build(installer, elevated: false);

        int code = CliTestHost.Invoke(root, stdout, stderr, "eventlog", "install");

        Assert.Equal(ExitCodes.ElevationRequired, code);
        Assert.Equal(0, installer.InstallCalls);
    }

    [Fact]
    public void Install_unsupported_platform_exits_3()
    {
        var installer = new FakeEventLogInstaller();
        var (root, stdout, stderr) = Build(installer, supported: false);

        int code = CliTestHost.Invoke(root, stdout, stderr, "eventlog", "install");

        Assert.Equal(ExitCodes.UnsupportedPlatform, code);
        Assert.Equal(0, installer.InstallCalls);
    }

    [Fact]
    public void Install_reports_a_registration_failure_as_error()
    {
        var installer = new FakeEventLogInstaller { ThrowOnInstall = new InvalidOperationException("denied") };
        var (root, stdout, stderr) = Build(installer);

        int code = CliTestHost.Invoke(root, stdout, stderr, "eventlog", "install");

        Assert.Equal(ExitCodes.Error, code);
        Assert.Contains("Could not register", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Uninstall_removes_the_source()
    {
        var installer = new FakeEventLogInstaller { Registered = true };
        var (root, stdout, stderr) = Build(installer);

        int code = CliTestHost.Invoke(root, stdout, stderr, "eventlog", "uninstall");

        Assert.Equal(ExitCodes.Success, code);
        Assert.False(installer.Registered);
        Assert.Contains("Removed Event Log source", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Uninstall_reports_when_absent()
    {
        var installer = new FakeEventLogInstaller { Registered = false };
        var (root, stdout, stderr) = Build(installer);

        int code = CliTestHost.Invoke(root, stdout, stderr, "eventlog", "uninstall");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("No Event Log source", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Uninstall_not_elevated_exits_5()
    {
        var installer = new FakeEventLogInstaller { Registered = true };
        var (root, stdout, stderr) = Build(installer, elevated: false);

        int code = CliTestHost.Invoke(root, stdout, stderr, "eventlog", "uninstall");

        Assert.Equal(ExitCodes.ElevationRequired, code);
        Assert.Equal(0, installer.UninstallCalls);
    }

    [Fact]
    public void Status_reports_registered()
    {
        var installer = new FakeEventLogInstaller { Registered = true };
        var (root, stdout, stderr) = Build(installer);

        int code = CliTestHost.Invoke(root, stdout, stderr, "eventlog", "status");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains(": registered", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Status_reports_not_registered_without_elevation()
    {
        var installer = new FakeEventLogInstaller { Registered = false };
        var (root, stdout, stderr) = Build(installer, elevated: false);

        int code = CliTestHost.Invoke(root, stdout, stderr, "eventlog", "status");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("not registered", stdout.ToString(), StringComparison.Ordinal);
    }
}
