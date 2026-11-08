namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// The supervised Windows Update repair: stop the update services for the duration of
/// the repair only (their start types are never changed), set the download cache
/// (<c>SoftwareDistribution</c>) and the catalogue database (<c>catroot2</c>) aside under
/// a dated name, then start the services that were running again. Windows rebuilds both
/// folders on the next scan; the set-aside copies stay so nothing is lost.
/// </summary>
public interface IUpdateRepair
{
    /// <summary>Runs the repair, or with <paramref name="whatIf"/> lists what it would do.</summary>
    UpdateRepairResult Repair(bool whatIf);
}
