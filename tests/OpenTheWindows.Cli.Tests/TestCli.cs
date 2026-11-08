using System.CommandLine;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Profiles;
using OpenTheWindows.Core.Releases;
using OpenTheWindows.Core.Updates;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

/// <summary>
/// Builds a <see cref="CliServices"/> for command tests, defaulting the scan
/// engine (over fake readers), the apply engine (over an in-memory machine), the
/// health probe and the elevation context so tests that do not exercise them need
/// not construct them.
/// </summary>
internal static class TestCli
{
    private static readonly DateTimeOffset FixedTime = new(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);

    public static CliServices Services(
        DoctorService doctor,
        Func<string?, CatalogLoadResult> loadCatalog,
        FakeReaders? readers = null,
        IMachineHealthProbe? health = null,
        Func<ApplyEngine>? createApplyEngine = null,
        Func<UpdateControlService>? createUpdateControl = null,
        Func<IUpdateHideService>? createUpdateHide = null,
        Func<IAppReleaseChecker>? createReleaseChecker = null,
        IElevationContext? elevation = null,
        IScheduledTaskInstaller? taskInstaller = null,
        IJournalStore? journalStore = null,
        IUserHiveEnumerator? userHiveEnumerator = null,
        IActiveSetupRegistrar? activeSetupRegistrar = null,
        IEventLogInstaller? eventLogInstaller = null,
        CliHardening? hardening = null)
    {
        FakeReaders scanReaders = readers ?? new FakeReaders();
        FakeScheduledTaskInstaller defaultInstaller = new();
        FakeJournalStore defaultJournal = new();
        FakeUserHiveEnumerator defaultUsers = new();
        FakeActiveSetupRegistrar defaultRegistrar = new();
        FakeEventLogInstaller defaultEventLog = new();
        Func<ApplyEngine> applyEngine = createApplyEngine ?? (() => FakeApplyEngine.Create(new FakeMachine()).Engine);
        return new CliServices(
            doctor,
            loadCatalog,
            () => FakeScanEngine.Create(scanReaders, FakeOperatingSystemInfo.Windows11Pro24H2(), FixedTime),
            applyEngine,
            createUpdateControl ?? (() => FakeUpdateControl.Create(new FakeMachine()).Service),
            createUpdateHide ?? (() => new FakeUpdateHideService()),
            createReleaseChecker ?? (() => new FakeAppReleaseChecker()),
            () => scanReaders,
            () => taskInstaller ?? defaultInstaller,
            () => journalStore ?? defaultJournal,
            () => userHiveEnumerator ?? defaultUsers,
            () => activeSetupRegistrar ?? defaultRegistrar,
            () => eventLogInstaller ?? defaultEventLog,
            health ?? new FakeHealthProbe([]),
            elevation ?? new FakeElevationContext(isElevated: true),
            hardening ?? Hardening(applyEngine));
    }

    /// <summary>
    /// Test hardening: an empty trust store, a folder preparer that just creates the
    /// folder, a guard that reports every executable location safe (unless given one),
    /// the same apply engine for per-user runs, and a temporary profile staging folder.
    /// </summary>
    public static CliHardening Hardening(
        Func<ApplyEngine> userScopeEngine, FakeExecutableLocationGuard? guard = null, ISecureDirectory? directories = null)
        => new(
            () => new DirectoryTrustedKeySource(EmptyTrustStore),
            directories ?? new FakeSecureDirectory(),
            guard ?? new FakeExecutableLocationGuard(),
            userScopeEngine,
            StagingDirectory);

    /// <summary>A trust store folder with no keys in it.</summary>
    public static string EmptyTrustStore { get; } = Directory.CreateTempSubdirectory("otw-cli-trust").FullName;

    /// <summary>The folder profile files are staged into for tasks and the logon fallback.</summary>
    public static string StagingDirectory { get; } = Directory.CreateTempSubdirectory("otw-cli-staging").FullName;

    /// <summary>
    /// A command host wired for the write commands (apply/remediate): the built-in
    /// catalogue, a doctor reporting <paramref name="supported"/>, the given apply
    /// <paramref name="engine"/> (default in-memory) and an elevation context set by
    /// <paramref name="elevated"/>. Shared so apply and remediate tests do not repeat it.
    /// </summary>
    public static (RootCommand Root, StringWriter Out, StringWriter Err) ApplyHost(
        bool supported = true, bool elevated = true, ApplyEngine? engine = null, FakeReaders? readers = null,
        IScheduledTaskInstaller? taskInstaller = null, IJournalStore? journalStore = null,
        IActiveSetupRegistrar? activeSetupRegistrar = null, IEventLogInstaller? eventLogInstaller = null,
        CliHardening? hardening = null)
    {
        var doctor = new DoctorService(
            new FakeOperatingSystemInfo(supported ? FakeOperatingSystemInfo.Windows11Pro24H2() : FakeOperatingSystemInfo.WindowsServer2025()),
            new FakeElevationContext(isElevated: true));
        return CliTestHost.Host(Services(
            doctor,
            _ => CatalogLoader.LoadBuiltIn(),
            readers,
            createApplyEngine: engine is null ? null : () => engine,
            elevation: new FakeElevationContext(elevated),
            taskInstaller: taskInstaller,
            journalStore: journalStore,
            activeSetupRegistrar: activeSetupRegistrar,
            eventLogInstaller: eventLogInstaller,
            hardening: hardening));
    }

    /// <summary>A command host wired with a specific user-hive enumerator for <c>otw users</c> tests.</summary>
    public static (RootCommand Root, StringWriter Out, StringWriter Err) UsersHost(IUserHiveEnumerator enumerator)
    {
        var doctor = new DoctorService(
            new FakeOperatingSystemInfo(FakeOperatingSystemInfo.Windows11Pro24H2()),
            new FakeElevationContext(isElevated: true));
        return CliTestHost.Host(Services(doctor, _ => CatalogLoader.LoadBuiltIn(), userHiveEnumerator: enumerator));
    }
}
