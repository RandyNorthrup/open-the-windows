using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Updates;
using OpenTheWindows.Windows;
using OpenTheWindows.Windows.Readers;
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
/// <param name="CreateRegistryReader">Builds a read-only registry reader (used to read the machine profile policy).</param>
/// <param name="CreateTaskInstaller">Builds the scheduled-task installer (drift remediation task).</param>
/// <param name="CreateJournalStore">Builds a journal store (read to compare the last-run OS build against the live one).</param>
/// <param name="CreateUserHiveEnumerator">Builds the user-hive enumerator (real user profiles an all-users apply targets).</param>
/// <param name="CreateActiveSetupRegistrar">Builds the Active Setup registrar (per-user logon fallback for an all-users apply).</param>
/// <param name="CreateEventLogInstaller">Builds the Event Log source installer (the <c>otw eventlog</c> command and MSI custom action).</param>
/// <param name="Health">The read-only machine health probe.</param>
/// <param name="Elevation">The process elevation context.</param>
internal sealed record CliServices(
    DoctorService Doctor,
    Func<string?, CatalogLoadResult> LoadCatalog,
    Func<ScanEngine> CreateScanEngine,
    Func<ApplyEngine> CreateApplyEngine,
    Func<UpdateControlService> CreateUpdateControl,
    Func<IRegistryReader> CreateRegistryReader,
    Func<IScheduledTaskInstaller> CreateTaskInstaller,
    Func<IJournalStore> CreateJournalStore,
    Func<IUserHiveEnumerator> CreateUserHiveEnumerator,
    Func<IActiveSetupRegistrar> CreateActiveSetupRegistrar,
    Func<IEventLogInstaller> CreateEventLogInstaller,
    IMachineHealthProbe Health,
    IElevationContext Elevation)
{
    /// <summary>Production services.</summary>
    public static CliServices CreateDefault()
        => new(
            new DoctorService(new WindowsOperatingSystemInfo(), new WindowsElevationContext()),
            directory => directory is null ? CatalogLoader.LoadBuiltIn() : CatalogLoader.LoadWithDirectory(directory),
            CreateDefaultScanEngine,
            CreateDefaultApplyEngine,
            static () => WindowsUpdateControlFactory.Create(),
            static () => new WindowsRegistryReader(),
            static () => new WindowsScheduledTaskInstaller(),
            static () => new WindowsJournalStore(),
            static () => new WindowsUserHiveEnumerator(),
            static () => new WindowsActiveSetupRegistrar(),
            static () => new WindowsEventLogInstaller(),
            new WindowsMachineHealthProbe(),
            new WindowsElevationContext());

    private static ScanEngine CreateDefaultScanEngine() => WindowsScanEngineFactory.Create();

    private static ApplyEngine CreateDefaultApplyEngine() => WindowsApplyEngineFactory.Create();
}
