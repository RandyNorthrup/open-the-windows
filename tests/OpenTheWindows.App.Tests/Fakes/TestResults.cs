using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.App.Tests.Fakes;

/// <summary>Builders for the engine results and journals the view-model tests script.</summary>
internal static class TestResults
{
    /// <summary>An apply result with no entries in <paramref name="state"/>.</summary>
    public static ApplyResult EmptyApplyResult(RunState state = RunState.Completed) => ApplyResult(state);

    /// <summary>An apply result in <paramref name="state"/> with the given entry outcomes.</summary>
    public static ApplyResult ApplyResult(RunState state, params EntryOutcome[] outcomes) => new(
        Guid.Empty,
        state,
        RestartRequirement.None,
        new RestorePointResult(RestorePointStatus.NotRequested, null, "test"),
        new PreflightReport([]),
        new PlanResult("test", [], []),
        outcomes);

    /// <summary>One entry outcome for <paramref name="id"/>.</summary>
    public static EntryOutcome Outcome(string id, ApplyState result, string? note = null)
        => new(new TweakId(id), result, RiskTier.Safe, RestartRequirement.None, note);

    /// <summary>A journal for <paramref name="runId"/> whose entries ended with the given results.</summary>
    public static JournalRecord Record(Guid runId, params (string Id, ApplyState Result)[] entries) => new()
    {
        RunId = runId,
        StartedAt = new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero),
        AppVersion = "test",
        Os = new JournalOs(26200, 1, "Professional"),
        Profile = "GUI Privacy",
        Operator = "tester",
        RestorePoint = new JournalRestorePoint(false, false, "test", null),
        Entries = [.. entries.Select(e => new JournalEntry { Id = e.Id, Actions = [], Result = e.Result })],
        Result = RunState.Completed,
    };
}
