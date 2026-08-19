using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.Core.Tests.Engine;

public sealed class AuditReportTaskTests
{
    private const string Exe = @"C:\Program Files\OpenTheWindows\otw.exe";
    private const string Drop = @"\\fileserver\audits\host.json";

    [Fact]
    public void Build_produces_the_system_hidden_audit_task()
    {
        ScheduledTaskSpec spec = AuditReportTask.Build("disa-stig-v2r7", TaskTrigger.Weekly, Exe, Drop);

        Assert.Equal(@"\OpenTheWindows\Audit", spec.TaskPath);
        Assert.Equal(Exe, spec.Executable);
        Assert.Equal("audit run --baseline \"disa-stig-v2r7\" --json --out \"" + Drop + "\"", spec.Arguments);
        Assert.Equal(TaskTrigger.Weekly, spec.Trigger);
        Assert.True(spec.RunAsSystem && spec.HighestPrivileges && spec.Hidden);
    }

    [Theory]
    [InlineData(TaskTrigger.Daily)]
    [InlineData(TaskTrigger.Weekly)]
    public void Build_preserves_the_trigger(TaskTrigger trigger)
        => Assert.Equal(trigger, AuditReportTask.Build("ms-baseline-25h2", trigger, Exe, Drop).Trigger);

    [Fact]
    public void Build_rejects_an_empty_baseline_id()
        => Assert.Throws<ArgumentException>(() => AuditReportTask.Build(string.Empty, TaskTrigger.Daily, Exe, Drop));

    [Fact]
    public void The_default_report_path_ends_in_the_program_data_reports_file()
        => Assert.EndsWith(Path.Combine("OpenTheWindows", "reports", "last-audit.json"), AuditReportTask.DefaultReportPath, StringComparison.Ordinal);
}
