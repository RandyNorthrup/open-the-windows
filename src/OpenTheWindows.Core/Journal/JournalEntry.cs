using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Journal;

/// <summary>One catalogue entry as journaled: its ordered actions and aggregate result.</summary>
public sealed class JournalEntry
{
    /// <summary>The catalogue entry id.</summary>
    public required string Id { get; init; }

    /// <summary>The entry's risk tier at the time of the run (so revert/history are self-contained).</summary>
    public RiskTier Risk { get; init; }

    /// <summary>The entry's restart requirement at the time of the run.</summary>
    public RestartRequirement Requires { get; init; }

    /// <summary>The entry's actions in apply order.</summary>
    public required IReadOnlyList<JournalAction> Actions { get; init; }

    /// <summary>The entry's aggregate outcome (worst of its actions).</summary>
    public ApplyState Result { get; set; }

    /// <summary>Whether the entry was applied over a managed setting under break-glass.</summary>
    public bool BreakGlass { get; set; }
}
