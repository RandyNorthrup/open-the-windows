using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>Builds a <see cref="ScanEngine"/> over a single <see cref="FakeReaders"/>.</summary>
public static class FakeScanEngine
{
    /// <summary>Creates an engine whose readers, OS facts and clock are all fakes.</summary>
    public static ScanEngine Create(FakeReaders readers, OperatingSystemFacts os, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(readers);
        return new ScanEngine(
            readers, readers, readers, readers, readers, readers, readers, readers, readers,
            new FakeOperatingSystemInfo(os), new FixedTimeProvider(at));
    }
}
