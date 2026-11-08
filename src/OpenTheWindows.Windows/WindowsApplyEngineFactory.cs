using System.Runtime.Versioning;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Windows.Readers;
using OpenTheWindows.Windows.Security;
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
    /// Builds the machine apply engine: the hardened ProgramData journal and audit
    /// trail and the machine-wide run lock. Pass a <paramref name="journalDirectory"/> /
    /// <paramref name="auditDirectory"/> to redirect the journal and audit trail to an
    /// unprotected folder (tests).
    /// </summary>
    public static ApplyEngine Create(string? journalDirectory = null, string? auditDirectory = null)
        => Build(
            journalDirectory is null
                ? new OwnershipTrackingJournalStore(new WindowsJournalStore(), new PolicyOwnershipLedger())
                : new WindowsJournalStore(journalDirectory),
            auditDirectory is null ? new WindowsAuditSink() : new WindowsAuditSink(auditDirectory),
            new WindowsRunLock());

    /// <summary>
    /// Builds the engine for an unelevated per-user run (the Active Setup logon fallback),
    /// which writes only the caller's own hive: its journal and audit trail live under the
    /// caller's <c>%LocalAppData%</c>, and it serialises on a per-session lock.
    /// </summary>
    public static ApplyEngine CreateForCurrentUser()
        => Build(
            new WindowsJournalStore(WindowsJournalStore.UserDirectory),
            new WindowsAuditSink(WindowsAuditSink.UserDirectory),
            new WindowsRunLock(WindowsRunLock.SessionLockName));

    private static ApplyEngine Build(IJournalStore journal, IAuditSink audit, IRunLock runLock)
    {
        var runner = new WindowsCommandRunner();
        var securityPolicy = new WindowsSecurityPolicyManager(runner);

        var readers = new StateReaders(
            new WindowsRegistryReader(),
            new WindowsServiceReader(),
            new WindowsScheduledTaskReader(),
            new WindowsAppxReader(),
            new WindowsOptionalFeatureReader(runner),
            new WindowsDefenderPreferenceReader(),
            new WindowsPowerSettingReader(),
            securityPolicy);

        var writers = new StateWriters(
            new WindowsRegistryWriter(),
            new WindowsServiceWriter(),
            new WindowsScheduledTaskWriter(),
            new WindowsAppxWriter(),
            new WindowsOptionalFeatureWriter(runner),
            new WindowsDefenderPreferenceWriter(),
            new WindowsPowerSettingWriter(),
            runner,
            securityPolicy);

        var os = new WindowsOperatingSystemInfo();
        var elevation = new WindowsElevationContext();
        var interactiveUser = new WindowsInteractiveUserResolver();
        var services = new ApplyServices(
            journal,
            new WindowsRestorePointService(),
            new WindowsExplorerRestarter(),
            new WindowsPreflight(elevation, os),
            audit,
            elevation,
            interactiveUser,
            new WindowsUserHiveEnumerator(),
            new WindowsUserHiveLoader(),
            runLock);

        var scan = new ScanEngine(
            readers.Registry, readers.Services, readers.Tasks, readers.Appx, readers.Features, readers.Defender,
            readers.Power, readers.SecurityPolicy, interactiveUser, new WindowsManagedSettingDetector(), os, TimeProvider.System);

        return new ApplyEngine(scan, new ActionApplier(readers, writers), services, os, TimeProvider.System);
    }
}
