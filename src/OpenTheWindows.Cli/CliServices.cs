using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Persistence;
using OpenTheWindows.Core.Privacy;
using OpenTheWindows.Core.Profiles;
using OpenTheWindows.Core.Releases;
using OpenTheWindows.Core.Startup;
using OpenTheWindows.Core.Updates;
using OpenTheWindows.Windows;
using OpenTheWindows.Windows.Readers;
using OpenTheWindows.Windows.Security;
using OpenTheWindows.Windows.Writers;

namespace OpenTheWindows.Cli;

/// <summary>
/// Services the command tree needs. Production wiring uses the real Windows
/// adapters and the embedded catalogue; tests supply fakes.
/// </summary>
/// <param name="Doctor">Pre-flight checker.</param>
/// <param name="LoadCatalog">Loads the catalogue; the argument is an optional override directory.</param>
/// <param name="CreateScanEngine">Builds a scan engine over the real state readers.</param>
/// <param name="CreateApplyEngine">Builds the transactional apply engine over the real readers, writers and services.</param>
/// <param name="CreateUpdateControl">Builds the Windows Update guardrail service over the real apply engine and adapters.</param>
/// <param name="CreateUpdateHide">Builds the per-update hide/unhide service over the Windows Update Agent.</param>
/// <param name="CreateUpdateTarget">Builds the feature-update target (hold-at-version) service.</param>
/// <param name="CreateUpdateRepair">Builds the supervised Windows Update repair.</param>
/// <param name="CreateReleaseChecker">Builds the opt-in GitHub Releases checker.</param>
/// <param name="CreateRegistryReader">Builds a read-only registry reader (used to read the machine profile policy).</param>
/// <param name="CreateTaskInstaller">Builds the scheduled-task installer (drift remediation task).</param>
/// <param name="CreateJournalStore">Builds a journal store (read to compare the last-run OS build against the live one).</param>
/// <param name="CreateUserHiveEnumerator">Builds the user-hive enumerator (real user profiles an all-users apply targets).</param>
/// <param name="CreateActiveSetupRegistrar">Builds the Active Setup registrar (per-user logon fallback for an all-users apply).</param>
/// <param name="CreateEventLogInstaller">Builds the Event Log source installer (the <c>otw eventlog</c> command and MSI custom action).</param>
/// <param name="CreateStartupApps">Builds the startup-apps service (the <c>otw startup</c> commands).</param>
/// <param name="CreateCapabilityAccess">Builds the read-only app-access view (the <c>otw access</c> command).</param>
/// <param name="CreatePersistenceWatch">Builds the persistence watch (the <c>otw watch</c> commands).</param>
/// <param name="Health">The read-only machine health probe.</param>
/// <param name="Elevation">The process elevation context.</param>
/// <param name="Hardening">The safeguards for privileged writes and trust decisions.</param>
internal sealed record CliServices(
    DoctorService Doctor,
    Func<string?, CatalogLoadResult> LoadCatalog,
    Func<ScanEngine> CreateScanEngine,
    Func<ApplyEngine> CreateApplyEngine,
    Func<UpdateControlService> CreateUpdateControl,
    Func<IUpdateHideService> CreateUpdateHide,
    Func<UpdateTargetService> CreateUpdateTarget,
    Func<IUpdateRepair> CreateUpdateRepair,
    Func<IAppReleaseChecker> CreateReleaseChecker,
    Func<IRegistryReader> CreateRegistryReader,
    Func<IScheduledTaskInstaller> CreateTaskInstaller,
    Func<IJournalStore> CreateJournalStore,
    Func<IUserHiveEnumerator> CreateUserHiveEnumerator,
    Func<IActiveSetupRegistrar> CreateActiveSetupRegistrar,
    Func<IEventLogInstaller> CreateEventLogInstaller,
    Func<StartupAppsService> CreateStartupApps,
    Func<CapabilityAccessService> CreateCapabilityAccess,
    Func<PersistenceWatchService> CreatePersistenceWatch,
    IMachineHealthProbe Health,
    IElevationContext Elevation,
    CliHardening Hardening)
{
    /// <summary>Builds the trusted-key source (shortcut for <see cref="CliHardening.CreateTrustedKeys"/>).</summary>
    public ITrustedKeySource CreateTrustedKeys() => Hardening.CreateTrustedKeys();

    /// <summary>Production services.</summary>
    public static CliServices CreateDefault()
        => new(
            new DoctorService(new WindowsOperatingSystemInfo(), new WindowsElevationContext()),
            LoadDefaultCatalog,
            CreateDefaultScanEngine,
            CreateDefaultApplyEngine,
            static () => WindowsUpdateControlFactory.Create(),
            static () => WindowsUpdateHideFactory.Create(),
            static () => WindowsUpdateControlFactory.CreateTarget(),
            static () => WindowsUpdateControlFactory.CreateRepair(),
            static () => GitHubAppReleaseChecker.ForProcess(),
            static () => new WindowsRegistryReader(),
            static () => new WindowsScheduledTaskInstaller(),
            static () => new WindowsJournalStore(),
            static () => new WindowsUserHiveEnumerator(),
            static () => new WindowsActiveSetupRegistrar(),
            static () => new WindowsEventLogInstaller(),
            static () => WindowsActivityFactory.CreateStartupApps(),
            static () => WindowsActivityFactory.CreateCapabilityAccess(),
            static () => WindowsActivityFactory.CreatePersistenceWatch(),
            new WindowsMachineHealthProbe(),
            new WindowsElevationContext(),
            new CliHardening(
                static () => new WindowsTrustedKeySource(),
                new WindowsSecureDirectory(),
                new WindowsExecutableLocationGuard(),
                static () => WindowsApplyEngineFactory.CreateForCurrentUser(),
                WindowsDataPaths.Profiles));

    /// <summary>
    /// Loads the built-in catalogue, plus an operator <c>--catalog-dir</c>. When the
    /// machine requires signed profiles, every operator catalogue file must carry a
    /// signature from the trust store over exactly the text that is loaded.
    /// </summary>
    private static CatalogLoadResult LoadDefaultCatalog(string? directory)
    {
        if (directory is null)
        {
            return CatalogLoader.LoadBuiltIn();
        }

        var policy = ProfilePolicy.Read(new WindowsRegistryReader());
        var keys = new WindowsTrustedKeySource();
        return CatalogLoader.LoadWithDirectory(directory, source => ProfileTrust.Enforce(source.Name, source.Json, policy, keys));
    }

    private static ScanEngine CreateDefaultScanEngine() => WindowsScanEngineFactory.Create();

    private static ApplyEngine CreateDefaultApplyEngine() => WindowsApplyEngineFactory.Create();
}
