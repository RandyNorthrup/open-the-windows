using OpenTheWindows.App.Services;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Audit;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.App.Tests.Services;

/// <summary>The audit coordinator scores a baseline via a real <see cref="AuditEngine"/> over fakes.</summary>
public sealed class AuditCoordinatorTests
{
    private static Baseline Baseline()
        => new(null, 1, "unit", "Unit Baseline",
            [new BaselineRule("R", "Secure Boot", BaselineSeverity.CatI, [], new BaselineCheck("boot.secure-boot"), [new Uri("https://example.test/x")])]);

    private static Func<AuditEngine> EngineFactory(HealthStatus status)
    {
        TweakCatalog catalog = CatalogLoader.LoadBuiltIn().Catalog!;
        var health = new FakeHealthProbe([new HealthCheckResult("boot.secure-boot", "Secure Boot", status, status.ToString(), null)]);
        return () => new AuditEngine(
            FakeScanEngine.Create(new FakeReaders(), FakeOperatingSystemInfo.Windows11Pro24H2(), DateTimeOffset.UnixEpoch),
            health,
            catalog);
    }

    [Fact]
    public async Task AuditAsync_scores_the_baseline_through_the_engine()
    {
        Baseline baseline = Baseline();
        var coordinator = new AuditCoordinator(EngineFactory(HealthStatus.Pass), [baseline]);

        AuditReport report = await coordinator.AuditAsync(baseline);

        Assert.Equal(100, report.Score);
        Assert.Equal([baseline], coordinator.Baselines);
    }

    [Fact]
    public async Task A_failing_check_lowers_the_score()
    {
        Baseline baseline = Baseline();
        var coordinator = new AuditCoordinator(EngineFactory(HealthStatus.Fail), [baseline]);

        AuditReport report = await coordinator.AuditAsync(baseline);

        Assert.Equal(0, report.Score);
    }

    [Fact]
    public void Rejects_null_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new AuditCoordinator(null!, []));
        Assert.Throws<ArgumentNullException>(() => new AuditCoordinator(EngineFactory(HealthStatus.Pass), null!));
    }
}
