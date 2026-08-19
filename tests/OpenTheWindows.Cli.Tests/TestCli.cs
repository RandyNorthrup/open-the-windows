using System.CommandLine;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
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
        IElevationContext? elevation = null,
        IScheduledTaskInstaller? taskInstaller = null,
        IJournalStore? journalStore = null,
        IUserHiveEnumerator? userHiveEnumerator = null,
        IActiveSetupRegistrar? activeSetupRegistrar = null)
    {
        FakeReaders scanReaders = readers ?? new FakeReaders();
        FakeScheduledTaskInstaller defaultInstaller = new();
        FakeJournalStore defaultJournal = new();
        FakeUserHiveEnumerator defaultUsers = new();
        FakeActiveSetupRegistrar defaultRegistrar = new();
        return new CliServices(
            doctor,
            loadCatalog,
            () => FakeScanEngine.Create(scanReaders, FakeOperatingSystemInfo.Windows11Pro24H2(), FixedTime),
            createApplyEngine ?? (() => FakeApplyEngine.Create(new FakeMachine()).Engine),
            createUpdateControl ?? (() => FakeUpdateControl.Create(new FakeMachine()).Service),
            () => scanReaders,
            () => taskInstaller ?? defaultInstaller,
            () => journalStore ?? defaultJournal,
            () => userHiveEnumerator ?? defaultUsers,
            () => activeSetupRegistrar ?? defaultRegistrar,
            health ?? new FakeHealthProbe([]),
            elevation ?? new FakeElevationContext(isElevated: true));
    }

    /// <summary>
    /// A command host wired for the write commands (apply/remediate): the built-in
    /// catalogue, a doctor reporting <paramref name="supported"/>, the given apply
    /// <paramref name="engine"/> (default in-memory) and an elevation context set by
    /// <paramref name="elevated"/>. Shared so apply and remediate tests do not repeat it.
    /// </summary>
    public static (RootCommand Root, StringWriter Out, StringWriter Err) ApplyHost(
        bool supported = true, bool elevated = true, ApplyEngine? engine = null, FakeReaders? readers = null,
        IScheduledTaskInstaller? taskInstaller = null, IJournalStore? journalStore = null,
        IActiveSetupRegistrar? activeSetupRegistrar = null)
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
            activeSetupRegistrar: activeSetupRegistrar));
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
