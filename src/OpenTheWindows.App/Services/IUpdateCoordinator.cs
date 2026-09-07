using OpenTheWindows.Core.Updates;

namespace OpenTheWindows.App.Services;

/// <summary>
/// The GUI's gateway to Windows Update control: the current status, whether the
/// pause is owned by organisation policy, and pause / resume run off the UI
/// thread. Abstracted so the Updates view model is testable with a fake.
/// </summary>
internal interface IUpdateCoordinator
{
    /// <summary>Reads the current update status (version, end-of-servicing, pause state, pin).</summary>
    UpdateStatus Status();

    /// <summary>Whether the pause is managed by MDM/Group Policy (a what-if pause plans as Managed).</summary>
    bool IsManaged();

    /// <summary>Pauses updates for <paramref name="days"/> days, on a background thread.</summary>
    Task<PauseOutcome> PauseAsync(int days, bool extended);

    /// <summary>Resumes updates, on a background thread.</summary>
    Task<ResumeOutcome> ResumeAsync();

    /// <summary>Lists updates the Windows Update Agent currently has hidden.</summary>
    Task<IReadOnlyList<DiscoveredUpdate>> ListHiddenAsync();

    /// <summary>Hides a discovered update by knowledge-base id, on a background thread.</summary>
    Task<UpdateHideOutcome> HideAsync(string knowledgeBaseId);

    /// <summary>Unhides a discovered update by knowledge-base id, on a background thread.</summary>
    Task<UpdateHideOutcome> UnhideAsync(string knowledgeBaseId);
}
