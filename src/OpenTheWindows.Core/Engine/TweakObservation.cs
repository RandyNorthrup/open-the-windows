using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Engine;

/// <summary>The observed compliance of one catalogue entry (the worst of its actions).</summary>
/// <param name="Id">The entry id.</param>
/// <param name="State">Aggregate compliance state.</param>
/// <param name="Risk">The entry's breakage risk tier (drives report severity).</param>
/// <param name="Actions">Per-action observations (empty when the entry does not apply).</param>
public sealed record TweakObservation(
    TweakId Id,
    ComplianceState State,
    RiskTier Risk,
    IReadOnlyList<ActionObservation> Actions);
