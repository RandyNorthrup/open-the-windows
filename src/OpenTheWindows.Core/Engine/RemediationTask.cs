using System.Globalization;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Core.Engine;

/// <summary>
/// Builds the <see cref="ScheduledTaskSpec"/> for the drift-remediation task
/// (<c>\OpenTheWindows\Remediate</c>): a SYSTEM, highest-privileges, hidden task
/// that runs <c>otw remediate</c> for a profile and writes its report to the
/// reports directory. Pure and deterministic so the produced spec is asserted in
/// tests without touching Task Scheduler.
/// </summary>
public static class RemediationTask
{
    /// <summary>The full Task Scheduler path of the remediation task.</summary>
    public const string TaskPath = @"\OpenTheWindows\Remediate";

    /// <summary>The default report file the task writes (<c>%ProgramData%\OpenTheWindows\reports\last-remediate.json</c>).</summary>
    public static string DefaultReportPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "OpenTheWindows",
        "reports",
        "last-remediate.json");

    /// <summary>
    /// Builds the task spec that runs
    /// <c>&lt;executablePath&gt; remediate --profile "&lt;profile&gt;" --json --out "&lt;reportPath&gt;"</c>
    /// on the given <paramref name="trigger"/>.
    /// </summary>
    public static ScheduledTaskSpec Build(string profile, TaskTrigger trigger, string executablePath, string reportPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(profile);
        ArgumentException.ThrowIfNullOrEmpty(executablePath);
        ArgumentException.ThrowIfNullOrEmpty(reportPath);

        string arguments = string.Create(
            CultureInfo.InvariantCulture,
            $"remediate --profile \"{profile}\" --json --out \"{reportPath}\"");
        string description = string.Create(
            CultureInfo.InvariantCulture,
            $"Open the Windows: re-enforce profile '{profile}' (drift remediation).");

        return new ScheduledTaskSpec(
            TaskPath,
            description,
            executablePath,
            arguments,
            trigger,
            RunAsSystem: true,
            HighestPrivileges: true,
            Hidden: true);
    }
}
