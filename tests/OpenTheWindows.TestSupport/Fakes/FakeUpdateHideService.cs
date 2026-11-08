using OpenTheWindows.Core.Updates;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>In-memory <see cref="IUpdateHideService"/> for CLI and engine tests.</summary>
public sealed class FakeUpdateHideService : IUpdateHideService
{
    /// <summary>Hidden updates <see cref="ListHidden"/> returns.</summary>
    public IList<DiscoveredUpdate> Hidden { get; } = [];

    /// <summary>The last hide id, when Hide was called.</summary>
    public string? LastHide { get; private set; }

    /// <summary>The last unhide id, when Unhide was called.</summary>
    public string? LastUnhide { get; private set; }

    /// <summary>What Hide/Unhide return. Null means a default success built from the id.</summary>
    public UpdateHideOutcome? OutcomeToReturn { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<DiscoveredUpdate> ListHidden() => [.. Hidden];

    /// <inheritdoc />
    public UpdateHideOutcome Hide(string knowledgeBaseId)
    {
        LastHide = knowledgeBaseId;
        return OutcomeToReturn ?? new UpdateHideOutcome(true, KnowledgeBaseId.Normalize(knowledgeBaseId) ?? knowledgeBaseId, 1, "Hid.");
    }

    /// <inheritdoc />
    public UpdateHideOutcome Preview(string knowledgeBaseId, bool hide)
    {
        LastPreview = (knowledgeBaseId, hide);
        return new UpdateHideOutcome(true, KnowledgeBaseId.Normalize(knowledgeBaseId) ?? knowledgeBaseId, 1, hide ? "Would hide." : "Would unhide.");
    }

    /// <summary>The last preview requested.</summary>
    public (string KnowledgeBaseId, bool Hide)? LastPreview { get; private set; }

    /// <inheritdoc />
    public UpdateHideOutcome Unhide(string knowledgeBaseId)
    {
        LastUnhide = knowledgeBaseId;
        return OutcomeToReturn ?? new UpdateHideOutcome(true, KnowledgeBaseId.Normalize(knowledgeBaseId) ?? knowledgeBaseId, 1, "Unhid.");
    }
}
