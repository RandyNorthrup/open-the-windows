using System.Runtime.Versioning;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Windows.Readers;
using OpenTheWindows.Windows.Writers;

namespace OpenTheWindows.Windows;

/// <summary>
/// Wires a production <see cref="ApplyEngine"/> over the real Windows readers,
/// writers and services. Shared by the CLI's service composition and by the VM
/// integration tests so the wiring exists in exactly one place.
/// </summary>
[SupportedOSPlatform("windows10.0.19041.0")]
public static class WindowsApplyEngineFactory
{
    /// <summary>
    /// Builds the apply engine. Pass a <paramref name="journalDirectory"/> /
    /// <paramref name="auditDirectory"/> to redirect the journal and audit trail
    /// (tests); leave them null to use the default ProgramData locations.
    /// </summary>
    public static ApplyEngine Create(string? journalDirectory = null, string? auditDirectory = null)
    {
        var readers = new StateReaders(
            new WindowsRegistryReader(),
            new WindowsServiceReader(),
            new WindowsScheduledTaskReader(),
            new WindowsAppxReader(),
            new WindowsOptionalFeatureReader(),
            new WindowsDefenderPreferenceReader(),
            new WindowsPowerSettingReader());

        var runner = new WindowsCommandRunner();
        var writers = new StateWriters(
            new WindowsRegistryWriter(),
            new WindowsServiceWriter(),
            new WindowsScheduledTaskWriter(),
            new WindowsAppxWriter(),
            new WindowsOptionalFeatureWriter(runner),
            new WindowsDefenderPreferenceWriter(),
            new WindowsPowerSettingWriter(),
            runner);

        var os = new WindowsOperatingSystemInfo();
        var elevation = new WindowsElevationContext();
        var interactiveUser = new WindowsInteractiveUserResolver();
        var services = new ApplyServices(
            journalDirectory is null ? new WindowsJournalStore() : new WindowsJournalStore(journalDirectory),
            new WindowsRestorePointService(),
            new WindowsExplorerRestarter(),
            new WindowsPreflight(elevation, os),
            auditDirectory is null ? new WindowsAuditSink() : new WindowsAuditSink(auditDirectory),
            elevation,
            interactiveUser);

        var scan = new ScanEngine(
            readers.Registry, readers.Services, readers.Tasks, readers.Appx, readers.Features, readers.Defender,
            readers.Power, interactiveUser, new WindowsManagedSettingDetector(), os, TimeProvider.System);

        return new ApplyEngine(scan, new ActionApplier(readers, writers), services, os, TimeProvider.System);
    }
}
