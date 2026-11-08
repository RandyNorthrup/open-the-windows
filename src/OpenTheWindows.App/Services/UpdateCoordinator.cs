using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Updates;

namespace OpenTheWindows.App.Services;

/// <summary>
/// <see cref="IUpdateCoordinator"/> over the production update-control, hide, target and
/// repair services. Each is built once, lazily, from the Windows factories; every call
/// runs on the thread pool.
/// </summary>
internal sealed class UpdateCoordinator : IUpdateCoordinator
{
    private readonly Lazy<UpdateControlService> _service;
    private readonly Lazy<IUpdateHideService> _hide;
    private readonly Lazy<UpdateTargetService> _target;
    private readonly Lazy<IUpdateRepair> _repair;

    /// <summary>Creates the coordinator over the update-control, hide, target and repair factories.</summary>
    public UpdateCoordinator(
        Func<UpdateControlService> factory, Func<IUpdateHideService> hideFactory, Func<UpdateTargetService> targetFactory, Func<IUpdateRepair> repairFactory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(hideFactory);
        ArgumentNullException.ThrowIfNull(targetFactory);
        ArgumentNullException.ThrowIfNull(repairFactory);
        _service = new Lazy<UpdateControlService>(factory);
        _hide = new Lazy<IUpdateHideService>(hideFactory);
        _target = new Lazy<UpdateTargetService>(targetFactory);
        _repair = new Lazy<IUpdateRepair>(repairFactory);
    }

    /// <inheritdoc />
    public Task<UpdateStatus> StatusAsync() => Task.Run(() => _service.Value.Status());

    /// <inheritdoc />
    public Task<bool> IsManagedAsync() => Task.Run(() =>
        _service.Value.Pause(days: 1, extended: false, whatIf: true).Result.Plan.Entries.Any(e => e.Disposition == PlanDisposition.Managed));

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

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> TargetVersionsAsync() => Task.Run(() => _target.Value.KnownVersions());

    /// <inheritdoc />
    public Task<ApplyResult> HoldAtAsync(string version) => Task.Run(() => _target.Value.Target(version, whatIf: false));

    /// <inheritdoc />
    public Task<IReadOnlyList<RevertResult>> ClearHoldAsync() => Task.Run(() => _target.Value.Clear(whatIf: false));

    /// <inheritdoc />
    public Task<UpdateRepairResult> RepairAsync() => Task.Run(() => _repair.Value.Repair(whatIf: false));
}
