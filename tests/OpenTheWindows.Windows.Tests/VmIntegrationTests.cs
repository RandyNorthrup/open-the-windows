using System.Security.Principal;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Windows.Readers;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// Integration checks meant to run elevated on the lab VM (research 04 §5 / the
/// M2 acceptance list). They are skipped unless <c>OTW_INTEGRATION=1</c>, so a
/// normal unelevated run is unaffected. Run with:
/// <c>OTW_INTEGRATION=1 dotnet test tests/OpenTheWindows.Windows.Tests</c>
/// (elevated).
/// </summary>
public sealed class VmIntegrationTests
{
    private static bool IsEnabled
        => string.Equals(Environment.GetEnvironmentVariable("OTW_INTEGRATION"), "1", StringComparison.Ordinal);

    private static void RequireIntegration()
        => Assert.SkipUnless(IsEnabled, "Integration test: set OTW_INTEGRATION=1 (elevated, lab VM) to run.");

    [Fact]
    public void Registry_reads_current_build_matching_the_os()
    {
        RequireIntegration();
        var reader = new WindowsRegistryReader();
        var os = new WindowsOperatingSystemInfo();

        RegistryValueSnapshot snapshot = reader.Read(
            RegistryHive.LocalMachine, null, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuild");

        Assert.True(snapshot.Exists);
        Assert.NotNull(snapshot.Data);
        // CurrentBuild is a REG_SZ; it equals the numeric build the OS facts report.
        string build = snapshot.Data.Value.GetString() ?? string.Empty;
        Assert.Equal(os.GetFacts().Build.ToString(System.Globalization.CultureInfo.InvariantCulture), build);
    }

    [Fact]
    public void Service_reads_the_print_spooler()
    {
        RequireIntegration();
        var reader = new WindowsServiceReader();

        ServiceSnapshot? snapshot = reader.Read("Spooler");

        Assert.NotNull(snapshot);
        Assert.Equal("Spooler", snapshot.Name);
    }

    [Fact]
    public void Task_reads_the_scheduled_defrag()
    {
        RequireIntegration();
        var reader = new WindowsScheduledTaskReader();

        ScheduledTaskSnapshot? snapshot = reader.Read(@"\Microsoft\Windows\Defrag\ScheduledDefrag");

        Assert.NotNull(snapshot);
        Assert.True(snapshot.Exists);
    }

    [Fact]
    public void Interactive_user_resolves_to_the_session_user()
    {
        RequireIntegration();
        var resolver = new WindowsInteractiveUserResolver();

        InteractiveUser? user = resolver.Resolve();

        Assert.NotNull(user);
        Assert.Equal(WindowsIdentity.GetCurrent().User!.Value, user.Sid);
    }

    [Fact]
    public void Managed_detection_is_not_managed_on_the_unmanaged_vm()
    {
        RequireIntegration();
        var detector = new WindowsManagedSettingDetector();

        ManagedState state = detector.Detect(
            RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry");

        Assert.Equal(ManagedState.NotManaged, state);
    }

    [Fact]
    public void Health_probe_reports_all_28_checks()
    {
        RequireIntegration();
        var probe = new WindowsMachineHealthProbe();

        IReadOnlyList<HealthCheckResult> results = probe.Run();

        Assert.Equal(28, results.Count);
        Assert.All(results, r => Assert.False(string.IsNullOrWhiteSpace(r.Detail)));
    }
}
