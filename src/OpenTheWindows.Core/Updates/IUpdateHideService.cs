namespace OpenTheWindows.Core.Updates;

/// <summary>
/// Hides and unhides individual Windows updates through the Windows Update
/// Agent. Every change is audited. The agent may contact Microsoft Update to
/// resolve a KB; that is the OS update channel, not this product's telemetry.
/// </summary>
public interface IUpdateHideService
{
    /// <summary>Lists updates the agent currently has marked hidden.</summary>
    IReadOnlyList<DiscoveredUpdate> ListHidden();

    /// <summary>Hides every discovered update whose KB list contains <paramref name="knowledgeBaseId"/>.</summary>
    UpdateHideOutcome Hide(string knowledgeBaseId);

    /// <summary>Clears the hidden flag on every discovered update whose KB list contains <paramref name="knowledgeBaseId"/>.</summary>
    UpdateHideOutcome Unhide(string knowledgeBaseId);
}
