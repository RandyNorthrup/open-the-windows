using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.App.Services;

/// <summary>
/// <see cref="IApplyCoordinator"/> over the production <see cref="ApplyEngine"/> and
/// <see cref="ScanEngine"/>. Both are built once, lazily, from the Windows factories;
/// every call that touches the machine or the journal runs on the thread pool so the UI
/// thread never blocks.
/// </summary>
internal sealed class ApplyCoordinator : IApplyCoordinator
{
    private readonly Lazy<ApplyEngine> _engine;
    private readonly Lazy<ScanEngine> _scan;
    private readonly IExplorerRestarter _explorer;
    private readonly IInteractiveUserResolver _interactiveUser;

    /// <summary>Creates the coordinator over the engine factories and the Explorer / interactive-user services.</summary>
    public ApplyCoordinator(
        Func<ApplyEngine> engineFactory, Func<ScanEngine> scanFactory, IExplorerRestarter explorer, IInteractiveUserResolver interactiveUser)
    {
        ArgumentNullException.ThrowIfNull(engineFactory);
        ArgumentNullException.ThrowIfNull(scanFactory);
        ArgumentNullException.ThrowIfNull(explorer);
        ArgumentNullException.ThrowIfNull(interactiveUser);
        _engine = new Lazy<ApplyEngine>(engineFactory);
        _scan = new Lazy<ScanEngine>(scanFactory);
        _explorer = explorer;
        _interactiveUser = interactiveUser;
    }

    /// <inheritdoc />
    public Task<PlanResult> PreviewAsync(IReadOnlyList<TweakDefinition> entries, ApplyOptions options)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(options);
        return Task.Run(() => _engine.Value.Plan(entries, options));
    }

    /// <inheritdoc />
    public Task<ScanReport> ScanAsync(IReadOnlyList<TweakDefinition> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return Task.Run(() => _scan.Value.Scan(entries, "GUI scan"));
    }

    /// <inheritdoc />
    public Task<ApplyResult> ApplyAsync(IReadOnlyList<TweakDefinition> entries, ApplyOptions options)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(options);
        return Task.Run(() => _engine.Value.Apply(entries, options));
    }

    /// <inheritdoc />
    public Task RestartExplorerAsync() => Task.Run(() =>
    {
        InteractiveUser? user = _interactiveUser.Resolve();
        if (user is not null)
        {
            _explorer.RestartForSession(user.Sid);
        }
    });

    /// <inheritdoc />
    public IReadOnlyList<JournalSummary> History() => _engine.Value.History();

    /// <inheritdoc />
    public Task<JournalRecord> LoadRunAsync(Guid runId) => Task.Run(() => _engine.Value.LoadRun(runId));

    /// <inheritdoc />
    public Task<RevertResult> RevertAsync(Guid runId, RevertOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Task.Run(() => _engine.Value.Revert(runId, options));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<(JournalSummary Run, RevertResult Result)>> RevertEverythingAsync(RevertOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Task.Run(() => _engine.Value.RevertEverything(options));
    }
}
