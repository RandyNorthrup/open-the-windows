using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Tests.Fakes;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.App.Tests.ViewModels;

/// <summary>Dashboard composition and quick-action navigation, with fakes (no UI thread, no OS).</summary>
public sealed class DashboardViewModelTests
{
    private static DashboardViewModel Build(FakeNavigationService navigation)
    {
        DoctorService doctor = new(
            new FakeOperatingSystemInfo(FakeOperatingSystemInfo.Windows11Pro24H2()),
            new FakeElevationContext(isElevated: true, processUserName: "tester"));
        return new DashboardViewModel(new DoctorViewModel(doctor), navigation);
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
    public void Rejects_null_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new DashboardViewModel(null!, new FakeNavigationService()));
    }
}
