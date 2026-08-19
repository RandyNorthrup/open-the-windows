using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.App.Tests.Fakes;

/// <summary>Shared builders for engine result records the coordinator fakes hand back.</summary>
internal static class TestResults
{
    /// <summary>An empty, no-op apply result in the given state.</summary>
    public static ApplyResult EmptyApplyResult(RunState state = RunState.Completed) => new(
        Guid.Empty,
        state,
        RestartRequirement.None,
        new RestorePointResult(RestorePointStatus.NotRequested, null, "test"),
        new PreflightReport([]),
        new PlanResult("test", [], []),
        []);
}
