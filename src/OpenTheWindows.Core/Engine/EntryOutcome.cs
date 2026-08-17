using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Engine;

/// <summary>The outcome of one entry in an apply or revert run.</summary>
/// <param name="Id">The catalogue entry id.</param>
/// <param name="Result">The entry's outcome.</param>
/// <param name="Risk">The entry's risk tier.</param>
/// <param name="Requires">The entry's restart requirement.</param>
/// <param name="Note">A human-readable explanation, when relevant.</param>
public sealed record EntryOutcome(
    TweakId Id,
    ApplyState Result,
    RiskTier Risk,
    RestartRequirement Requires,
    string? Note);
