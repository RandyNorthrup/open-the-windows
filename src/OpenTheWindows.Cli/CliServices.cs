using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Updates;
using OpenTheWindows.Windows;
using OpenTheWindows.Windows.Readers;

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
/// <param name="Health">The read-only machine health probe.</param>
/// <param name="Elevation">The process elevation context.</param>
internal sealed record CliServices(
    DoctorService Doctor,
    Func<string?, CatalogLoadResult> LoadCatalog,
    Func<ScanEngine> CreateScanEngine,
    Func<ApplyEngine> CreateApplyEngine,
    Func<UpdateControlService> CreateUpdateControl,
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
            new WindowsMachineHealthProbe(),
            new WindowsElevationContext());

    private static ScanEngine CreateDefaultScanEngine()
        => new(
            new WindowsRegistryReader(),
            new WindowsServiceReader(),
            new WindowsScheduledTaskReader(),
            new WindowsAppxReader(),
            new WindowsOptionalFeatureReader(),
            new WindowsDefenderPreferenceReader(),
            new WindowsPowerSettingReader(),
            new WindowsInteractiveUserResolver(),
            new WindowsManagedSettingDetector(),
            new WindowsOperatingSystemInfo(),
            TimeProvider.System);

    private static ApplyEngine CreateDefaultApplyEngine() => WindowsApplyEngineFactory.Create();
}
