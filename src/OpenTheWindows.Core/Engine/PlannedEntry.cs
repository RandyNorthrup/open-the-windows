using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Engine;

/// <summary>One entry as an apply plan sees it: what will happen and why.</summary>
/// <param name="Id">The catalogue entry id.</param>
/// <param name="Disposition">What the plan intends to do with the entry.</param>
/// <param name="Risk">The entry's risk tier.</param>
/// <param name="Requires">The entry's restart requirement.</param>
/// <param name="Actions">The entry's actions with current compliance.</param>
/// <param name="Note">A human-readable explanation for a non-apply disposition.</param>
public sealed record PlannedEntry(
    TweakId Id,
    PlanDisposition Disposition,
    RiskTier Risk,
    RestartRequirement Requires,
    IReadOnlyList<PlannedAction> Actions,
    string? Note);
