namespace OpenTheWindows.Core.Journal;

/// <summary>A one-line summary of a journaled run, for <c>otw history</c>.</summary>
/// <param name="RunId">The run identifier.</param>
/// <param name="StartedAt">When the run started.</param>
/// <param name="Profile">The profile the run used.</param>
/// <param name="Result">The run's overall state.</param>
/// <param name="EntryCount">Number of entries in the run.</param>
/// <param name="RevertOf">The run this run reverted, if any.</param>
public sealed record JournalSummary(
    Guid RunId,
    DateTimeOffset StartedAt,
    string Profile,
    RunState Result,
    int EntryCount,
    Guid? RevertOf);
