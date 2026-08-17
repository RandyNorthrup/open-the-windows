using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.TestSupport.Fakes;

/// <summary>Returns a fixed set of health-check results for tests.</summary>
public sealed class FakeHealthProbe(IReadOnlyList<HealthCheckResult> results) : IMachineHealthProbe
{
    public IReadOnlyList<HealthCheckResult> Run() => results;
}
