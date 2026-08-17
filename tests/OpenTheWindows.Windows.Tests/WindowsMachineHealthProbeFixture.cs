using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Windows.Readers;

namespace OpenTheWindows.Windows.Tests;

/// <summary>Runs the health probe once and shares its results (WMI is slow).</summary>
public sealed class WindowsMachineHealthProbeFixture
{
    public WindowsMachineHealthProbeFixture() => Results = new WindowsMachineHealthProbe().Run();

    public IReadOnlyList<HealthCheckResult> Results { get; }
}
