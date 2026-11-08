using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Persistence;
using OpenTheWindows.Core.Privacy;
using OpenTheWindows.Core.Startup;

namespace OpenTheWindows.App.Services;

/// <summary>
/// The GUI's gateway to the startup apps, the app-access view and the persistence watch, all
/// off the UI thread. Abstracted so the Startup and App access pages are testable with a fake.
/// </summary>
internal interface IActivityCoordinator
{
    /// <summary>The startup apps with their on/off state.</summary>
    Task<IReadOnlyList<StartupApp>> ListStartupAsync();

    /// <summary>Turns a startup app on or off (a journaled, revertable run).</summary>
    Task<ApplyResult> SetStartupEnabledAsync(StartupApp app, bool enabled);

    /// <summary>Recent camera, microphone and location use (every capability when <paramref name="all"/>).</summary>
    Task<IReadOnlyList<CapabilityUse>> RecentAccessAsync(bool all);

    /// <summary>Records every autostart now as the persistence baseline.</summary>
    Task<PersistenceSnapshot> TakeSnapshotAsync();

    /// <summary>Compares the autostarts now with the baseline; <see langword="null"/> when there is none.</summary>
    Task<PersistenceComparison?> CompareAsync();
}
