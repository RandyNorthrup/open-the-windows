using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Persistence;
using OpenTheWindows.Core.Privacy;
using OpenTheWindows.Core.Startup;

namespace OpenTheWindows.App.Services;

/// <summary>
/// <see cref="IActivityCoordinator"/> over the production services, each built once, lazily,
/// from its factory; every call runs on the thread pool.
/// </summary>
internal sealed class ActivityCoordinator : IActivityCoordinator
{
    private readonly Lazy<StartupAppsService> _startup;
    private readonly Lazy<CapabilityAccessService> _access;
    private readonly Lazy<PersistenceWatchService> _watch;

    /// <summary>Creates the coordinator over the startup, access and watch factories.</summary>
    public ActivityCoordinator(
        Func<StartupAppsService> startupFactory, Func<CapabilityAccessService> accessFactory, Func<PersistenceWatchService> watchFactory)
    {
        ArgumentNullException.ThrowIfNull(startupFactory);
        ArgumentNullException.ThrowIfNull(accessFactory);
        ArgumentNullException.ThrowIfNull(watchFactory);
        _startup = new Lazy<StartupAppsService>(startupFactory);
        _access = new Lazy<CapabilityAccessService>(accessFactory);
        _watch = new Lazy<PersistenceWatchService>(watchFactory);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StartupApp>> ListStartupAsync() => Task.Run(() => _startup.Value.List());

    /// <inheritdoc />
    public Task<ApplyResult> SetStartupEnabledAsync(StartupApp app, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(app);
        return Task.Run(() => _startup.Value.SetEnabled(app, enabled, whatIf: false));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CapabilityUse>> RecentAccessAsync(bool all)
        => Task.Run(() => _access.Value.Recent(all ? null : CapabilityAccessService.DefaultCapabilities));

    /// <inheritdoc />
    public Task<PersistenceSnapshot> TakeSnapshotAsync() => Task.Run(() => _watch.Value.TakeSnapshot());

    /// <inheritdoc />
    public Task<PersistenceComparison?> CompareAsync() => Task.Run(() => _watch.Value.Compare());
}
