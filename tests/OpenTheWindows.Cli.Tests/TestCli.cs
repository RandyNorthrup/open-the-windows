using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Cli.Tests;

/// <summary>
/// Builds a <see cref="CliServices"/> for command tests, defaulting the scan
/// engine (over fake readers) and health probe so tests that do not exercise
/// them need not construct them.
/// </summary>
internal static class TestCli
{
    private static readonly DateTimeOffset FixedTime = new(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);

    public static CliServices Services(
        DoctorService doctor,
        Func<string?, CatalogLoadResult> loadCatalog,
        FakeReaders? readers = null,
        IMachineHealthProbe? health = null)
    {
        FakeReaders scanReaders = readers ?? new FakeReaders();
        return new CliServices(
            doctor,
            loadCatalog,
            () => FakeScanEngine.Create(scanReaders, FakeOperatingSystemInfo.Windows11Pro24H2(), FixedTime),
            health ?? new FakeHealthProbe([]));
    }
}
