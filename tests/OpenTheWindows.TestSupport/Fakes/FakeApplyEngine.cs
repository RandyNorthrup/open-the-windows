using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>Wires an <see cref="ApplyEngine"/> and its scan engine over a single <see cref="FakeMachine"/>.</summary>
public static class FakeApplyEngine
{
    /// <summary>The fixed instant the fake engine and its dependents use when no clock is given.</summary>
    public static readonly DateTimeOffset DefaultInstant = new(2026, 8, 17, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Builds the engine and returns a harness exposing every fake for assertions.</summary>
    /// <param name="machine">The in-memory machine (create it with the same <paramref name="trace"/> to assert write ordering).</param>
    /// <param name="os">OS facts (defaults to Windows 11 Pro 24H2).</param>
    /// <param name="at">Fixed clock instant.</param>
    /// <param name="trace">Optional shared list the journal store and machine append their operations to.</param>
    /// <param name="time">Optional clock; when given it overrides <paramref name="at"/> (use a <see cref="MutableTimeProvider"/> to give successive runs distinct timestamps).</param>
    /// <param name="userHives">Optional user-hive enumerator for all-users apply (defaults to an empty enumerator).</param>
    /// <param name="hiveLoader">Optional user-hive loader for all-users apply (defaults to a recording no-op loader).</param>
    public static ApplyHarness Create(
        FakeMachine machine, OperatingSystemFacts? os = null, DateTimeOffset? at = null, IList<string>? trace = null,
        TimeProvider? time = null, IUserHiveEnumerator? userHives = null, IUserHiveLoader? hiveLoader = null)
    {
        ArgumentNullException.ThrowIfNull(machine);

        var osInfo = new FakeOperatingSystemInfo(os ?? FakeOperatingSystemInfo.Windows11Pro24H2());
        TimeProvider clock = time ?? new FixedTimeProvider(at ?? DefaultInstant);
        var readers = new StateReaders(machine, machine, machine, machine, machine, machine, machine, machine);
        var writers = new StateWriters(machine, machine, machine, machine, machine, machine, machine, machine, machine);
        var scan = new ScanEngine(machine, machine, machine, machine, machine, machine, machine, machine, machine, machine, osInfo, clock);
        var applier = new ActionApplier(readers, writers);
        var journal = new FakeJournalStore { Trace = trace };
        var audit = new FakeAuditSink();
        var restore = new FakeRestorePointService();
        var explorer = new FakeExplorerRestarter();
        var preflight = new FakePreflight();
        var elevation = new FakeElevationContext(isElevated: true);
        var services = new ApplyServices(
            journal, restore, explorer, preflight, audit, elevation, machine,
            userHives ?? new FakeUserHiveEnumerator(), hiveLoader ?? new FakeUserHiveLoader());
        var engine = new ApplyEngine(scan, applier, services, osInfo, clock);

        return new ApplyHarness(engine, scan, machine, journal, audit, restore, explorer, preflight, elevation);
    }
}
