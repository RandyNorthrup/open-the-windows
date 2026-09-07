using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Updates;

namespace OpenTheWindows.App.Services;

/// <summary>
/// <see cref="IUpdateCoordinator"/> over the production
/// <see cref="UpdateControlService"/>. The service is built once, lazily, from
/// the Windows factory; pause and resume run on the thread pool.
/// </summary>
internal sealed class UpdateCoordinator : IUpdateCoordinator
{
    private readonly Lazy<UpdateControlService> _service;
    private readonly Lazy<IUpdateHideService> _hide;

    /// <summary>Creates the coordinator over the update-control and hide factories.</summary>
    public UpdateCoordinator(Func<UpdateControlService> factory, Func<IUpdateHideService> hideFactory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(hideFactory);
        _service = new Lazy<UpdateControlService>(factory);
        _hide = new Lazy<IUpdateHideService>(hideFactory);
    }

    /// <inheritdoc />
    public UpdateStatus Status() => _service.Value.Status();

    /// <inheritdoc />
    public bool IsManaged()
    {
        PauseOutcome outcome = _service.Value.Pause(days: 1, extended: false, whatIf: true);
        return outcome.Result.Plan.Entries.Any(e => e.Disposition == PlanDisposition.Managed);
    }

    /// <inheritdoc />
    public Task<PauseOutcome> PauseAsync(int days, bool extended) => Task.Run(() => _service.Value.Pause(days, extended, whatIf: false));

    /// <inheritdoc />
    public Task<ResumeOutcome> ResumeAsync() => Task.Run(() => _service.Value.Resume(whatIf: false));

    /// <inheritdoc />
    public Task<IReadOnlyList<DiscoveredUpdate>> ListHiddenAsync() => Task.Run(() => _hide.Value.ListHidden());

    /// <inheritdoc />
    public Task<UpdateHideOutcome> HideAsync(string knowledgeBaseId) => Task.Run(() => _hide.Value.Hide(knowledgeBaseId));

    /// <inheritdoc />
    public Task<UpdateHideOutcome> UnhideAsync(string knowledgeBaseId) => Task.Run(() => _hide.Value.Unhide(knowledgeBaseId));
}
