using System.CommandLine;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

public sealed class TaskCommandTests
{
    private static (RootCommand Root, StringWriter Out, StringWriter Err) Build(
        FakeScheduledTaskInstaller installer, bool supported = true, bool elevated = true)
        => TestCli.ApplyHost(supported, elevated, taskInstaller: installer);

    [Fact]
    public void Install_unsupported_platform_exits_3()
    {
        var installer = new FakeScheduledTaskInstaller();
        var (root, stdout, stderr) = Build(installer, supported: false);

        int code = CliTestHost.Invoke(root, stdout, stderr, "task", "install", "--profile", "home");

        Assert.Equal(ExitCodes.UnsupportedPlatform, code);
        Assert.Empty(installer.Installed);
    }

    [Fact]
    public void Install_not_elevated_exits_5()
    {
        var installer = new FakeScheduledTaskInstaller();
        var (root, stdout, stderr) = Build(installer, elevated: false);

        int code = CliTestHost.Invoke(root, stdout, stderr, "task", "install", "--profile", "home");

        Assert.Equal(ExitCodes.ElevationRequired, code);
        Assert.Empty(installer.Installed);
    }

    [Fact]
    public void Install_unknown_profile_exits_4()
    {
        var installer = new FakeScheduledTaskInstaller();
        var (root, stdout, stderr) = Build(installer);

        int code = CliTestHost.Invoke(root, stdout, stderr, "task", "install", "--profile", "nope");

        Assert.Equal(ExitCodes.InvalidInput, code);
        Assert.Empty(installer.Installed);
    }

    [Fact]
    public void Install_conflicting_triggers_exits_4()
    {
        var installer = new FakeScheduledTaskInstaller();
        var (root, stdout, stderr) = Build(installer);

        int code = CliTestHost.Invoke(root, stdout, stderr, "task", "install", "--profile", "home", "--daily", "--weekly");

        Assert.Equal(ExitCodes.InvalidInput, code);
        Assert.Empty(installer.Installed);
    }

    [Fact]
    public void Install_registers_the_remediation_task()
    {
        var installer = new FakeScheduledTaskInstaller();
        var (root, stdout, stderr) = Build(installer);

        int code = CliTestHost.Invoke(root, stdout, stderr, "task", "install", "--profile", "home", "--weekly");

        Assert.Equal(ExitCodes.Success, code);
        ScheduledTaskSpec spec = Assert.Single(installer.Installed);
        Assert.Equal(RemediationTask.TaskPath, spec.TaskPath);
        Assert.Equal(TaskTrigger.Weekly, spec.Trigger);
        Assert.Contains("remediate --profile \"home\"", spec.Arguments, StringComparison.Ordinal);
        Assert.True(spec.RunAsSystem && spec.Hidden && spec.HighestPrivileges);
    }

    [Fact]
    public void Install_defaults_to_a_daily_trigger()
    {
        var installer = new FakeScheduledTaskInstaller();
        var (root, stdout, stderr) = Build(installer);

        CliTestHost.Invoke(root, stdout, stderr, "task", "install", "--profile", "basic");

        Assert.Equal(TaskTrigger.Daily, Assert.Single(installer.Installed).Trigger);
    }

    [Fact]
    public void Remove_deletes_the_task()
    {
        var installer = new FakeScheduledTaskInstaller();
        var (root, stdout, stderr) = Build(installer);

        int code = CliTestHost.Invoke(root, stdout, stderr, "task", "remove");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Equal(RemediationTask.TaskPath, Assert.Single(installer.Removed));
        Assert.Contains("Removed task", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Remove_reports_when_no_task_exists()
    {
        var installer = new FakeScheduledTaskInstaller { RemoveResult = false };
        var (root, stdout, stderr) = Build(installer);

        int code = CliTestHost.Invoke(root, stdout, stderr, "task", "remove");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("No task", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Remove_not_elevated_exits_5()
    {
        var installer = new FakeScheduledTaskInstaller();
        var (root, stdout, stderr) = Build(installer, elevated: false);

        int code = CliTestHost.Invoke(root, stdout, stderr, "task", "remove");

        Assert.Equal(ExitCodes.ElevationRequired, code);
        Assert.Empty(installer.Removed);
    }
}
