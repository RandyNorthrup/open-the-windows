using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Persistence;
using OpenTheWindows.Core.Privacy;
using OpenTheWindows.Core.Startup;

namespace OpenTheWindows.App.Tests.Fakes;

/// <summary>Scriptable <see cref="IActivityCoordinator"/>: returns what the test sets, records the calls, and can fault.</summary>
internal sealed class FakeActivityCoordinator : IActivityCoordinator
{
    /// <summary>The startup apps listed.</summary>
    public List<StartupApp> Apps { get; } = [];

    /// <summary>The result a startup toggle returns.</summary>
    public ApplyResult ToggleResult { get; set; } = TestResults.EmptyApplyResult();

    /// <summary>The uses listed.</summary>
    public List<CapabilityUse> Uses { get; } = [];

    /// <summary>The comparison <see cref="CompareAsync"/> returns.</summary>
    public PersistenceComparison? Comparison { get; set; }

    /// <summary>When set, every call faults.</summary>
    public bool Throw { get; set; }

    /// <summary>The last toggle requested.</summary>
    public (StartupApp App, bool Enabled)? LastToggle { get; private set; }

    /// <summary>The <c>all</c> flag of the last access read.</summary>
    public bool? LastAll { get; private set; }

    /// <summary>How many snapshots were taken.</summary>
    public int SnapshotCount { get; private set; }

    /// <inheritdoc />
    public Task<IReadOnlyList<StartupApp>> ListStartupAsync() => ResultAsync<IReadOnlyList<StartupApp>>([.. Apps]);

    /// <inheritdoc />
    public Task<ApplyResult> SetStartupEnabledAsync(StartupApp app, bool enabled)
    {
        LastToggle = (app, enabled);
        return ResultAsync(ToggleResult);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CapabilityUse>> RecentAccessAsync(bool all)
    {
        LastAll = all;
        return ResultAsync<IReadOnlyList<CapabilityUse>>([.. Uses]);
    }

    /// <inheritdoc />
    public Task<PersistenceSnapshot> TakeSnapshotAsync()
    {
        SnapshotCount++;
        return ResultAsync(new PersistenceSnapshot(PersistenceSnapshot.CurrentSchemaVersion, new DateTimeOffset(2026, 11, 7, 9, 0, 0, TimeSpan.Zero), [new PersistenceItem(PersistenceKind.Service, "A", "x")]));
    }

    /// <inheritdoc />
    public Task<PersistenceComparison?> CompareAsync() => ResultAsync(Comparison);

    private Task<T> ResultAsync<T>(T value) => Throw ? Task.FromException<T>(new InvalidOperationException("activity failed")) : Task.FromResult(value);
}
