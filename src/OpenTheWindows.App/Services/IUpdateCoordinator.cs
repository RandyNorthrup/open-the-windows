using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Updates;

namespace OpenTheWindows.App.Services;

/// <summary>
/// The GUI's gateway to Windows Update control: the current status, whether the pause is
/// owned by organisation policy, pause / resume, per-update hide, the feature-update
/// hold and the supervised repair — all off the UI thread. Abstracted so the Updates
/// view model is testable with a fake.
/// </summary>
internal interface IUpdateCoordinator
{
    /// <summary>Reads the current update status (version, end-of-servicing, pause state, pin).</summary>
    Task<UpdateStatus> StatusAsync();

    /// <summary>Whether the pause is managed by MDM/Group Policy (a what-if pause plans as Managed).</summary>
    Task<bool> IsManagedAsync();

    /// <summary>Pauses updates for <paramref name="days"/> days.</summary>
    Task<PauseOutcome> PauseAsync(int days, bool extended);

    /// <summary>Resumes updates (also ending a pause set in the Settings app).</summary>
    Task<ResumeOutcome> ResumeAsync();

    /// <summary>Lists updates the Windows Update Agent currently has hidden.</summary>
    Task<IReadOnlyList<DiscoveredUpdate>> ListHiddenAsync();

    /// <summary>Hides a discovered update by knowledge-base id.</summary>
    Task<UpdateHideOutcome> HideAsync(string knowledgeBaseId);

    /// <summary>Unhides a discovered update by knowledge-base id.</summary>
    Task<UpdateHideOutcome> UnhideAsync(string knowledgeBaseId);

    /// <summary>The Windows 11 versions this device can be held at.</summary>
    Task<IReadOnlyList<string>> TargetVersionsAsync();

    /// <summary>Holds feature updates at <paramref name="version"/>.</summary>
    Task<ApplyResult> HoldAtAsync(string version);

    /// <summary>Clears every active feature-update hold.</summary>
    Task<IReadOnlyList<RevertResult>> ClearHoldAsync();

    /// <summary>Runs the supervised Windows Update repair.</summary>
    Task<UpdateRepairResult> RepairAsync();
}
