using OpenTheWindows.Core.Audit;

namespace OpenTheWindows.App.Services;

/// <summary>
/// <see cref="IAuditCoordinator"/> over the production <see cref="AuditEngine"/>.
/// The engine is built once, lazily, from the factory (off the DI-validation
/// path); each audit runs on the thread pool so the UI thread never blocks.
/// </summary>
internal sealed class AuditCoordinator : IAuditCoordinator
{
    private readonly Lazy<AuditEngine> _engine;

    /// <summary>Creates the coordinator over the audit-engine factory and the built-in baselines.</summary>
    public AuditCoordinator(Func<AuditEngine> engineFactory, IReadOnlyList<Baseline> baselines)
    {
        ArgumentNullException.ThrowIfNull(engineFactory);
        ArgumentNullException.ThrowIfNull(baselines);
        _engine = new Lazy<AuditEngine>(engineFactory);
        Baselines = baselines;
    }

    /// <inheritdoc />
    public IReadOnlyList<Baseline> Baselines { get; }

    /// <inheritdoc />
    public Task<AuditReport> AuditAsync(Baseline baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        return Task.Run(() => _engine.Value.Audit(baseline));
    }
}
