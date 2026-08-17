using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// Tests the pre-flight's deterministic logic (elevation and supported-OS gating)
/// with fake elevation and OS facts; the registry-backed checks read the real
/// machine and are exercised for "does not throw".
/// </summary>
public sealed class WindowsPreflightTests
{
    private static WindowsPreflight Build(bool elevated, OperatingSystemFacts os)
        => new(new FakeElevationContext(elevated), new FakeOperatingSystemInfo(os));

    [Fact]
    public void Unelevated_process_is_blocked_on_elevation()
    {
        PreflightReport report = Build(elevated: false, FakeOperatingSystemInfo.Windows11Pro24H2()).Run();

        Assert.Contains(report.Issues, i => i.Check == PreflightCheck.Elevation && i.Blocking);
        Assert.False(report.CanProceed);
    }

    [Fact]
    public void Server_os_is_blocked_on_supported_os()
    {
        PreflightReport report = Build(elevated: true, FakeOperatingSystemInfo.WindowsServer2025()).Run();

        Assert.Contains(report.Issues, i => i.Check == PreflightCheck.SupportedOs && i.Blocking);
    }

    [Fact]
    public void Elevated_windows11_reports_no_elevation_or_os_issue()
    {
        PreflightReport report = Build(elevated: true, FakeOperatingSystemInfo.Windows11Pro24H2()).Run();

        Assert.DoesNotContain(report.Issues, i => i.Check == PreflightCheck.Elevation);
        Assert.DoesNotContain(report.Issues, i => i.Check == PreflightCheck.SupportedOs);
    }
}
