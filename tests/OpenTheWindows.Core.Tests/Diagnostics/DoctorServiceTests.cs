using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Core.Tests.Diagnostics;

public sealed class DoctorServiceTests
{
    [Fact]
    public void Windows11_elevated_is_supported_and_can_apply()
    {
        var sut = new DoctorService(
            new FakeOperatingSystemInfo(FakeOperatingSystemInfo.Windows11Pro24H2()),
            new FakeElevationContext(isElevated: true));

        SystemReport report = sut.Run();

        Assert.Equal(SupportStatus.Supported, report.Status);
        Assert.True(report.IsElevated);
        Assert.True(report.CanApply);
        Assert.Empty(report.Notes);
        Assert.Equal("26100.4351", report.OperatingSystem.FullBuild);
        Assert.Equal(AppInfo.Version, report.AppVersion);
    }

    [Fact]
    public void Windows11_not_elevated_is_supported_but_cannot_apply()
    {
        var sut = new DoctorService(
            new FakeOperatingSystemInfo(FakeOperatingSystemInfo.Windows11Pro24H2()),
            new FakeElevationContext(isElevated: false));

        SystemReport report = sut.Run();

        Assert.Equal(SupportStatus.Supported, report.Status);
        Assert.False(report.CanApply);
        string note = Assert.Single(report.Notes);
        Assert.Contains("Not running elevated", note, StringComparison.Ordinal);
    }

    [Fact]
    public void Windows10_is_unsupported_even_when_elevated()
    {
        var sut = new DoctorService(
            new FakeOperatingSystemInfo(FakeOperatingSystemInfo.Windows10Pro22H2()),
            new FakeElevationContext(isElevated: true));

        SystemReport report = sut.Run();

        Assert.Equal(SupportStatus.UnsupportedWindowsVersion, report.Status);
        Assert.False(report.CanApply);
        string note = Assert.Single(report.Notes);
        Assert.Contains("19045.5011", note, StringComparison.Ordinal);
    }

    [Fact]
    public void Server_sku_is_unsupported()
    {
        var sut = new DoctorService(
            new FakeOperatingSystemInfo(FakeOperatingSystemInfo.WindowsServer2025()),
            new FakeElevationContext(isElevated: true));

        SystemReport report = sut.Run();

        Assert.Equal(SupportStatus.UnsupportedServerSku, report.Status);
        Assert.False(report.CanApply);
        Assert.False(report.OperatingSystem.IsWindows11);
        Assert.Contains(report.Notes, n => n.Contains("Windows Server", StringComparison.Ordinal));
    }

    [Fact]
    public void Unsupported_and_not_elevated_reports_both_notes()
    {
        var sut = new DoctorService(
            new FakeOperatingSystemInfo(FakeOperatingSystemInfo.Windows10Pro22H2()),
            new FakeElevationContext(isElevated: false));

        SystemReport report = sut.Run();

        Assert.Equal(2, report.Notes.Count);
    }

    [Fact]
    public void Constructor_rejects_null_dependencies()
    {
        Assert.Throws<ArgumentNullException>(
            () => new DoctorService(null!, new FakeElevationContext(isElevated: true)));
        Assert.Throws<ArgumentNullException>(
            () => new DoctorService(new FakeOperatingSystemInfo(FakeOperatingSystemInfo.Windows11Pro24H2()), null!));
    }
}
