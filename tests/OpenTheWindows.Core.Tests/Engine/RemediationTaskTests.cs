using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.Core.Tests.Engine;

public sealed class RemediationTaskTests
{
    private const string Exe = @"C:\Program Files\OpenTheWindows\otw.exe";
    private const string Report = @"C:\ProgramData\OpenTheWindows\reports\last-remediate.json";

    [Fact]
    public void Build_produces_the_system_hidden_remediate_task()
    {
        ScheduledTaskSpec spec = RemediationTask.Build("home", TaskTrigger.Daily, Exe, Report);

        Assert.Equal(@"\OpenTheWindows\Remediate", spec.TaskPath);
        Assert.Equal(Exe, spec.Executable);
        Assert.Equal("remediate --profile \"home\" --json --out \"" + Report + "\"", spec.Arguments);
        Assert.Equal(TaskTrigger.Daily, spec.Trigger);
        Assert.True(spec.RunAsSystem);
        Assert.True(spec.HighestPrivileges);
        Assert.True(spec.Hidden);
    }

    [Theory]
    [InlineData(TaskTrigger.Daily)]
    [InlineData(TaskTrigger.Weekly)]
    [InlineData(TaskTrigger.Logon)]
    [InlineData(TaskTrigger.BuildChange)]
    public void Build_preserves_the_trigger(TaskTrigger trigger)
        => Assert.Equal(trigger, RemediationTask.Build("home", trigger, Exe, Report).Trigger);

    [Fact]
    public void Build_change_trigger_adds_the_if_build_changed_flag()
        => Assert.Equal(
            "remediate --profile \"home\" --if-build-changed --json --out \"" + Report + "\"",
            RemediationTask.Build("home", TaskTrigger.BuildChange, Exe, Report).Arguments);

    [Fact]
    public void Non_build_change_triggers_omit_the_if_build_changed_flag()
        => Assert.DoesNotContain(
            "--if-build-changed",
            RemediationTask.Build("home", TaskTrigger.Daily, Exe, Report).Arguments,
            StringComparison.Ordinal);

    [Fact]
    public void Build_rejects_an_empty_profile()
        => Assert.Throws<ArgumentException>(() => RemediationTask.Build(string.Empty, TaskTrigger.Daily, Exe, Report));

    [Fact]
    public void Default_report_path_is_the_program_data_reports_file()
        => Assert.EndsWith(
            Path.Combine("OpenTheWindows", "reports", "last-remediate.json"),
            RemediationTask.DefaultReportPath,
            StringComparison.Ordinal);
}
