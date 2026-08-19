using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.App.Services;

/// <summary>
/// <see cref="IApplyCoordinator"/> over the production <see cref="ApplyEngine"/>.
/// The engine is built once, lazily, from the Windows factory; apply and the
/// Explorer restart run on the thread pool so the UI thread never blocks.
/// </summary>
internal sealed class ApplyCoordinator : IApplyCoordinator
{
    private readonly Lazy<ApplyEngine> _engine;
    private readonly IExplorerRestarter _explorer;
    private readonly IInteractiveUserResolver _interactiveUser;

    /// <summary>Creates the coordinator over the engine factory and the Explorer / interactive-user services.</summary>
    public ApplyCoordinator(Func<ApplyEngine> engineFactory, IExplorerRestarter explorer, IInteractiveUserResolver interactiveUser)
    {
        ArgumentNullException.ThrowIfNull(engineFactory);
        ArgumentNullException.ThrowIfNull(explorer);
        ArgumentNullException.ThrowIfNull(interactiveUser);
        _engine = new Lazy<ApplyEngine>(engineFactory);
        _explorer = explorer;
        _interactiveUser = interactiveUser;
    }

    /// <inheritdoc />
    public PlanResult Preview(IReadOnlyList<TweakDefinition> entries, ApplyOptions options)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(options);
        return _engine.Value.Plan(entries, options);
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
    public Task<RevertResult> RevertAsync(Guid runId, RevertOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Task.Run(() => _engine.Value.Revert(runId, options));
    }
}
