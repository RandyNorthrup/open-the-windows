using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Services;
using OpenTheWindows.App.Tests.Fakes;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Audit;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.App.Tests.ViewModels;

/// <summary>Dashboard composition, quick-action navigation and baseline scoring, with fakes (no UI thread, no OS).</summary>
public sealed class DashboardViewModelTests
{
    private static DashboardViewModel Build(FakeNavigationService navigation, IAuditCoordinator? audit = null)
    {
        DoctorService doctor = new(
            new FakeOperatingSystemInfo(FakeOperatingSystemInfo.Windows11Pro24H2()),
            new FakeElevationContext(isElevated: true, processUserName: "tester"));
        return new DashboardViewModel(new DoctorViewModel(doctor), navigation, audit ?? new FakeAuditCoordinator());
    }

    private static Baseline Baseline(string id)
        => new(null, 1, id, id + " title",
            [new BaselineRule("R", "title", BaselineSeverity.CatI, [], new BaselineCheck("boot.secure-boot"), [new Uri("https://example.test/x")])]);

    private static AuditReport Report(string id, int score)
    {
        var os = new OperatingSystemFacts("Windows 11 Pro", "Professional", "24H2", 26100, 4351, "x64", "Client");
        AuditRuleResult[] results = [new("R", "title", BaselineSeverity.CatI, score == 100 ? AuditOutcome.Pass : AuditOutcome.Fail, [], "evidence")];
        return new AuditReport(id, id + " title", DateTimeOffset.UnixEpoch, os, score, AuditSummary.From(results), results);
    }

    [Fact]
    public void Is_the_dashboard_page_and_exposes_the_health_panel()
    {
        DashboardViewModel dashboard = Build(new FakeNavigationService());

        Assert.Equal(PageKeys.Dashboard, dashboard.Key);
        Assert.NotNull(dashboard.Doctor);
        Assert.False(string.IsNullOrEmpty(dashboard.Title));
    }

    [Fact]
    public void Open_navigates_to_the_requested_page()
    {
        FakeNavigationService navigation = new();
        DashboardViewModel dashboard = Build(navigation);

        dashboard.OpenCommand.Execute(PageKeys.About);

        Assert.Equal([PageKeys.About], navigation.Navigations);
    }

    [Fact]
    public void Scoring_does_not_run_until_the_user_asks()
    {
        var audit = new FakeAuditCoordinator
        {
            Baselines = [Baseline("a")],
            OnAudit = b => Report(b.Id, 50),
        };
        DashboardViewModel dashboard = Build(new FakeNavigationService(), audit);

        Assert.Empty(dashboard.BaselineScores);
        Assert.Equal(0, audit.AuditCount);
        Assert.Contains("Score this machine", dashboard.AuditStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refresh_scores_populates_a_score_per_baseline()
    {
        var audit = new FakeAuditCoordinator
        {
            Baselines = [Baseline("a"), Baseline("b")],
            OnAudit = b => Report(b.Id, string.Equals(b.Id, "a", StringComparison.Ordinal) ? 80 : 40),
        };
        DashboardViewModel dashboard = Build(new FakeNavigationService(), audit);

        await dashboard.RefreshScoresCommand.ExecuteAsync(null);

        Assert.Equal(2, audit.AuditCount);
        Assert.Equal(2, dashboard.BaselineScores.Count);
        Assert.Equal(80, dashboard.BaselineScores[0].Score);
        Assert.Equal("80 / 100", dashboard.BaselineScores[0].ScoreLabel);
        Assert.False(dashboard.IsBusy);
        Assert.True(dashboard.CanRefresh);
    }

    [Fact]
    public void Rejects_null_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new DashboardViewModel(null!, new FakeNavigationService(), new FakeAuditCoordinator()));
        DoctorService doctor = new(
            new FakeOperatingSystemInfo(FakeOperatingSystemInfo.Windows11Pro24H2()),
            new FakeElevationContext(isElevated: true));
        Assert.Throws<ArgumentNullException>(() => new DashboardViewModel(new DoctorViewModel(doctor), new FakeNavigationService(), null!));
    }
}
