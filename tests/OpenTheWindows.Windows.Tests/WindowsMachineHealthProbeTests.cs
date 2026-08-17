using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// Runs the read-only health probe once (WMI is slow) and shares the results
/// across the assertions. The registry-backed checks must return a real verdict;
/// checks whose source needs elevation may report <see cref="HealthStatus.Unknown"/>.
/// </summary>
public sealed class WindowsMachineHealthProbeTests : IClassFixture<WindowsMachineHealthProbeFixture>
{
    private readonly WindowsMachineHealthProbeFixture _fixture;

    public WindowsMachineHealthProbeTests(WindowsMachineHealthProbeFixture fixture) => _fixture = fixture;

    [Fact]
    public void Runs_all_28_checks_with_details()
    {
        IReadOnlyList<HealthCheckResult> results = _fixture.Results;

        Assert.Equal(28, results.Count);
        Assert.Equal(28, results.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(results, r => Assert.False(string.IsNullOrWhiteSpace(r.Id)));
        Assert.All(results, r => Assert.False(string.IsNullOrWhiteSpace(r.Title)));
        Assert.All(results, r => Assert.False(string.IsNullOrWhiteSpace(r.Detail)));
    }

    [Theory]
    [InlineData("boot.secure-boot")]
    [InlineData("update.build-support")]
    [InlineData("edition.support")]
    [InlineData("firewall.profiles")]
    public void Registry_backed_checks_return_a_real_verdict(string id)
    {
        HealthCheckResult result = _fixture.Results.Single(r => string.Equals(r.Id, id, StringComparison.Ordinal));

        Assert.NotEqual(HealthStatus.Unknown, result.Status);
    }
}
