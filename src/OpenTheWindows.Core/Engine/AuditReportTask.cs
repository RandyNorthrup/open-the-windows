using System.Globalization;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Core.Engine;

/// <summary>
/// Builds the <see cref="ScheduledTaskSpec"/> for the audit-report task
/// (<c>\OpenTheWindows\Audit</c>): a SYSTEM, highest-privileges, hidden task that
/// runs <c>otw audit run</c> for a baseline and writes a JSON report to a drop path
/// (typically a fleet UNC share) for collection. Pure and deterministic so the
/// produced spec is asserted in tests without touching Task Scheduler.
/// </summary>
public static class AuditReportTask
{
    /// <summary>The full Task Scheduler path of the audit-report task.</summary>
    public const string TaskPath = @"\OpenTheWindows\Audit";

    /// <summary>The default report file the task writes (<c>%ProgramData%\OpenTheWindows\reports\last-audit.json</c>).</summary>
    public static string DefaultReportPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "OpenTheWindows",
        "reports",
        "last-audit.json");

    /// <summary>
    /// Builds the task spec that runs
    /// <c>&lt;executablePath&gt; audit run --baseline "&lt;baselineId&gt;" --json --out "&lt;reportPath&gt;"</c>
    /// on the given <paramref name="trigger"/>.
    /// </summary>
    public static ScheduledTaskSpec Build(string baselineId, TaskTrigger trigger, string executablePath, string reportPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(baselineId);
        ArgumentException.ThrowIfNullOrEmpty(executablePath);
        ArgumentException.ThrowIfNullOrEmpty(reportPath);

        string arguments = string.Create(
            CultureInfo.InvariantCulture,
            $"audit run --baseline \"{baselineId}\" --json --out \"{reportPath}\"");
        string description = string.Create(
            CultureInfo.InvariantCulture,
            $"Open the Windows: audit this machine against baseline '{baselineId}' and drop the report.");

        return ScheduledTasks.System(TaskPath, description, executablePath, arguments, trigger);
    }
}
