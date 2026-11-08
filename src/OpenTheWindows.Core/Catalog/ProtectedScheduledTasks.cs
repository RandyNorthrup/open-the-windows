using System.Globalization;

namespace OpenTheWindows.Core.Catalog;

/// <summary>
/// Task Scheduler folders whose tasks the engine never disables: Windows Update
/// orchestration and self-repair, Defender, licensing, TPM and BitLocker
/// maintenance. Disabling them breaks servicing or security the same way a
/// Disabled start type on their services would. Enabling a task is always allowed.
/// </summary>
public static class ProtectedScheduledTasks
{
    /// <summary>Folder prefixes (with trailing separator) whose tasks must stay enabled.</summary>
    public static IReadOnlyList<string> Folders { get; } =
    [
        @"\Microsoft\Windows\WindowsUpdate\",
        @"\Microsoft\Windows\UpdateOrchestrator\",
        @"\Microsoft\Windows\WaaSMedic\",
        @"\Microsoft\Windows\InstallService\",
        @"\Microsoft\Windows\Windows Defender\",
        @"\Microsoft\Windows\ExploitGuard\",
        @"\Microsoft\Windows\SoftwareProtectionPlatform\",
        @"\Microsoft\Windows\TPM\",
        @"\Microsoft\Windows\BitLocker\",
    ];

    /// <summary>
    /// Returns why setting the task at <paramref name="taskPath"/> to
    /// <paramref name="enabled"/> is refused, or <see langword="null"/> when allowed.
    /// </summary>
    public static string? Refusal(string taskPath, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(taskPath);
        if (enabled)
        {
            return null;
        }

        string path = taskPath.Replace('/', '\\');
        string? folder = Folders.FirstOrDefault(f => path.StartsWith(f, StringComparison.OrdinalIgnoreCase));
        return folder is null
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"Task '{taskPath}' belongs to '{folder}', which keeps Windows serviced and protected; it is never disabled.");
    }
}
