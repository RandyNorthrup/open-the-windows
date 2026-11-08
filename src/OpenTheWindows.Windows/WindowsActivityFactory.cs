using System.Runtime.Versioning;
using OpenTheWindows.Core.Persistence;
using OpenTheWindows.Core.Privacy;
using OpenTheWindows.Core.Startup;
using OpenTheWindows.Windows.Readers;
using OpenTheWindows.Windows.Security;

namespace OpenTheWindows.Windows;

/// <summary>
/// Builds the production startup-apps, app-access and persistence-watch services: the
/// Windows readers for each, the interactive-user resolver (so the signed-in user's entries
/// are read from their own hive), and the journaled apply engine for startup changes.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public static class WindowsActivityFactory
{
    /// <summary>The startup-apps service over the real registry, Startup folders and apply engine.</summary>
    public static StartupAppsService CreateStartupApps()
        => new(new WindowsStartupSource(), new WindowsRegistryReader(), new WindowsInteractiveUserResolver(), WindowsApplyEngineFactory.Create(), TimeProvider.System);

    /// <summary>The read-only app-access view over the real ConsentStore.</summary>
    public static CapabilityAccessService CreateCapabilityAccess()
        => new(new WindowsConsentStoreSource(), new WindowsInteractiveUserResolver());

    /// <summary>The persistence watch over the real autostarts and the protected baseline file.</summary>
    public static PersistenceWatchService CreatePersistenceWatch()
        => new(new WindowsPersistenceSource(), new PersistenceSnapshotStore(), new WindowsInteractiveUserResolver(), TimeProvider.System);
}
