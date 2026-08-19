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

    /// <summary>Creates the coordinator over the update-control factory.</summary>
    public UpdateCoordinator(Func<UpdateControlService> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _service = new Lazy<UpdateControlService>(factory);
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
}
