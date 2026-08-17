using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>Wires an <see cref="ApplyEngine"/> and its scan engine over a single <see cref="FakeMachine"/>.</summary>
public static class FakeApplyEngine
{
    private static readonly DateTimeOffset DefaultInstant = new(2026, 8, 17, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Builds the engine and returns a harness exposing every fake for assertions.</summary>
    /// <param name="machine">The in-memory machine (create it with the same <paramref name="trace"/> to assert write ordering).</param>
    /// <param name="os">OS facts (defaults to Windows 11 Pro 24H2).</param>
    /// <param name="at">Fixed clock instant.</param>
    /// <param name="trace">Optional shared list the journal store and machine append their operations to.</param>
    public static ApplyHarness Create(
        FakeMachine machine, OperatingSystemFacts? os = null, DateTimeOffset? at = null, IList<string>? trace = null)
    {
        ArgumentNullException.ThrowIfNull(machine);

        var osInfo = new FakeOperatingSystemInfo(os ?? FakeOperatingSystemInfo.Windows11Pro24H2());
        var time = new FixedTimeProvider(at ?? DefaultInstant);
        var readers = new StateReaders(machine, machine, machine, machine, machine, machine, machine);
        var writers = new StateWriters(machine, machine, machine, machine, machine, machine, machine, machine);
        var scan = new ScanEngine(machine, machine, machine, machine, machine, machine, machine, machine, machine, osInfo, time);
        var applier = new ActionApplier(readers, writers);
        var journal = new FakeJournalStore { Trace = trace };
        var audit = new FakeAuditSink();
        var restore = new FakeRestorePointService();
        var explorer = new FakeExplorerRestarter();
        var preflight = new FakePreflight();
        var elevation = new FakeElevationContext(isElevated: true);
        var services = new ApplyServices(journal, restore, explorer, preflight, audit, elevation, machine);
        var engine = new ApplyEngine(scan, applier, services, osInfo, time);

        return new ApplyHarness(engine, scan, machine, journal, audit, restore, explorer, preflight, elevation);
    }
}
