using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.Core.Engine;
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
        IElevationContext? elevation = null)
    {
        FakeReaders scanReaders = readers ?? new FakeReaders();
        return new CliServices(
            doctor,
            loadCatalog,
            () => FakeScanEngine.Create(scanReaders, FakeOperatingSystemInfo.Windows11Pro24H2(), FixedTime),
            createApplyEngine ?? (() => FakeApplyEngine.Create(new FakeMachine()).Engine),
            createUpdateControl ?? (() => FakeUpdateControl.Create(new FakeMachine()).Service),
            health ?? new FakeHealthProbe([]),
            elevation ?? new FakeElevationContext(isElevated: true));
    }
}
