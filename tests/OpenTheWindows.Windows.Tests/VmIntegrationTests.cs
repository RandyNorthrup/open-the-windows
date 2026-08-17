using System.Security.Principal;
using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Model;
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

    [Fact]
    public void Apply_verify_gpupdate_and_revert_a_machine_policy_round_trips()
    {
        RequireIntegration();

        // Machine-scope policy value: exercises the Local GPO write path and gpupdate survival,
        // with no per-user hive (unavailable over a non-console session) and no Appx variance.
        const string PolicyPath = @"SOFTWARE\Policies\OpenTheWindows\IntegrationTest";
        string journalDir = Path.Combine(Path.GetTempPath(), "otw-int-" + Guid.NewGuid().ToString("N"));
        var reader = new WindowsRegistryReader();
        try
        {
            ApplyEngine engine = RealEngine(journalDir);
            TweakDefinition entry = RegistryEntry(PolicyPath, "Sample", RegistryValueKind.Policy);

            // Criterion 1: apply -> verify.
            ApplyResult applied = engine.Apply([entry], new ApplyOptions(
                WhatIf: false, CreateRestorePoint: false, BreakGlass: false, AllowAdvanced: true, AllowBreaking: true, "integration", RestartExplorer: false));
            Assert.Equal(RunState.Completed, applied.State);
            Assert.Equal(1, reader.Read(RegistryHive.LocalMachine, null, PolicyPath, "Sample").Data!.Value.GetInt32());

            // Criterion 3: the policy value survives gpupdate /force (it was written to the Local GPO).
            RunGpupdate();
            Assert.Equal(1, reader.Read(RegistryHive.LocalMachine, null, PolicyPath, "Sample").Data!.Value.GetInt32());

            // Criterion 1: revert -> the value is absent again.
            RevertResult reverted = engine.Revert(applied.RunId, new RevertOptions(WhatIf: false, RestartExplorer: false));
            Assert.Equal(RunState.Completed, reverted.State);
            Assert.False(reader.Read(RegistryHive.LocalMachine, null, PolicyPath, "Sample").Exists);
        }
        finally
        {
            using Microsoft.Win32.RegistryKey? policies = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\OpenTheWindows", writable: true);
            policies?.DeleteSubKeyTree("IntegrationTest", throwOnMissingSubKey: false);
            if (Directory.Exists(journalDir))
            {
                Directory.Delete(journalDir, recursive: true);
            }
        }
    }

    private static void RunGpupdate()
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "gpupdate.exe"),
            Arguments = "/target:computer /force",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        process!.WaitForExit((int)TimeSpan.FromMinutes(2).TotalMilliseconds);
    }

    private static ApplyEngine RealEngine(string journalDir)
        => WindowsApplyEngineFactory.Create(journalDir, Path.Combine(journalDir, "audit"));

    private static TweakDefinition RegistryEntry(string path, string name, RegistryValueKind valueKind)
    {
        var action = new RegistryAction(
            RegistryHive.LocalMachine, path, name, RegistryValueType.Dword,
            JsonSerializer.SerializeToElement(1), JsonSerializer.SerializeToElement(0), valueKind);

        return new TweakDefinition(
            new TweakId("integration.registry.sample"),
            "Integration sample",
            "A registry preference used only by the integration test.",
            "Verifies apply/revert against the real registry.",
            Category.Privacy,
            Level.Basic,
            RiskTier.Safe,
            Scope.Machine,
            TweakStatus.Verified,
            Applicability.Any,
            [action],
            RestartRequirement.None,
            [],
            [],
            null,
            [],
            [new Uri("https://github.com/RandyNorthrup/open-the-windows")],
            [],
            []);
    }
}
