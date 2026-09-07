namespace OpenTheWindows.Core.Updates;

/// <summary>One update the Windows Update Agent has discovered.</summary>
/// <param name="Identity">The WUA update identity (stable across hide/unhide).</param>
/// <param name="Title">The display title from the agent.</param>
/// <param name="KnowledgeBaseIds">KB article numbers without the <c>KB</c> prefix.</param>
/// <param name="IsHidden">Whether the agent will skip offering this update.</param>
public sealed record DiscoveredUpdate(
    string Identity,
    string Title,
    IReadOnlyList<string> KnowledgeBaseIds,
    bool IsHidden);
