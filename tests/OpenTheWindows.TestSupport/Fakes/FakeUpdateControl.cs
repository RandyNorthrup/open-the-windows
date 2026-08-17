using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Updates;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>
/// Wires an <see cref="UpdateControlService"/> over a single <see cref="FakeMachine"/>
/// and its apply harness, so pause/resume/status can be exercised end to end
/// against the in-memory machine and journal.
/// </summary>
public static class FakeUpdateControl
{
    /// <summary>Builds the update-control service and returns it with the apply harness for assertions.</summary>
    public static (UpdateControlService Service, ApplyHarness Harness) Create(
        FakeMachine machine,
        OperatingSystemFacts? os = null,
        DateTimeOffset? at = null,
        EndOfServicingCalendar? calendar = null,
        int extendedCeilingDays = PauseCalculator.DefaultExtendedCeilingDays)
    {
        ArgumentNullException.ThrowIfNull(machine);

        DateTimeOffset instant = at ?? FakeApplyEngine.DefaultInstant;
        ApplyHarness harness = FakeApplyEngine.Create(machine, os, instant);
        var osInfo = new FakeOperatingSystemInfo(os ?? FakeOperatingSystemInfo.Windows11Pro24H2());
        var service = new UpdateControlService(
            harness.Engine,
            machine,
            machine,
            harness.Audit,
            osInfo,
            calendar ?? EndOfServicingCalendar.LoadBuiltIn(),
            new FixedTimeProvider(instant),
            extendedCeilingDays);
        return (service, harness);
    }
}
