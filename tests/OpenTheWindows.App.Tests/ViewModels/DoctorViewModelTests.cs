using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core.Diagnostics;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.App.Tests.ViewModels;

public sealed class DoctorViewModelTests
{
    [Fact]
    public void Constructor_runs_doctor_and_populates_summaries()
    {
        var doctor = new DoctorService(
            new FakeOperatingSystemInfo(FakeOperatingSystemInfo.Windows11Pro24H2()),
            new FakeElevationContext(isElevated: true, processUserName: @"PC\admin"));

        var vm = new DoctorViewModel(doctor);

        Assert.Equal("Windows 11 Pro 24H2 (build 26100.4351, x64)", vm.OperatingSystemSummary);
        Assert.Equal("Professional / Client", vm.EditionSummary);
        Assert.Equal(@"PC\admin (elevated)", vm.UserSummary);
        Assert.Equal(SupportStatus.Supported, vm.Status);
        Assert.True(vm.CanApply);
        Assert.Empty(vm.Notes);
    }

    [Fact]
    public void Refresh_reflects_changed_facts_and_raises_property_changed()
    {
        var os = new FakeOperatingSystemInfo(FakeOperatingSystemInfo.Windows11Pro24H2());
        var elevation = new FakeElevationContext(isElevated: false);
        var vm = new DoctorViewModel(new DoctorService(os, elevation));
        List<string?> changed = [];
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.False(vm.CanApply);
        Assert.Single(vm.Notes);

        elevation.IsElevated = true;
        vm.RefreshCommand.Execute(null);

        Assert.True(vm.CanApply);
        Assert.Empty(vm.Notes);
        Assert.Contains(nameof(DoctorViewModel.CanApply), changed, StringComparer.Ordinal);
        Assert.Contains(nameof(DoctorViewModel.UserSummary), changed, StringComparer.Ordinal);
    }

    [Fact]
    public void Unsupported_platform_surfaces_status_and_notes()
    {
        var vm = new DoctorViewModel(new DoctorService(
            new FakeOperatingSystemInfo(FakeOperatingSystemInfo.WindowsServer2025()),
            new FakeElevationContext(isElevated: true)));

        Assert.Equal(SupportStatus.UnsupportedServerSku, vm.Status);
        Assert.False(vm.CanApply);
        Assert.Single(vm.Notes);
    }

    [Fact]
    public void Constructor_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => new DoctorViewModel(null!));
    }
}
