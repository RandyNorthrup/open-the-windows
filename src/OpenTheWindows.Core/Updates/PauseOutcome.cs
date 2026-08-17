using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.Core.Updates;

/// <summary>
/// The result of <c>otw updates pause</c>: the computed plan and the journaled,
/// reversible apply run that wrote it.
/// </summary>
/// <param name="Plan">The pause window and the values written.</param>
/// <param name="Result">The apply run (journal id, state, per-entry outcomes).</param>
/// <param name="Extended">Whether this was an extended pause beyond the 35-day cap (an Event 4002 was written).</param>
public sealed record PauseOutcome(PausePlan Plan, ApplyResult Result, bool Extended);
