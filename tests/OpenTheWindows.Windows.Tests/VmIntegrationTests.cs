using System.Security.Principal;
using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Windows.Readers;
using OpenTheWindows.Windows.Writers;

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

    [Fact]
    public void User_hive_loader_loads_and_unloads_the_default_profile()
    {
        RequireIntegration();

        // The Default profile hive is always present and never loaded — a safe target
        // to prove RegLoadKey/RegUnLoadKey (load, confirm mounted, read, unload) without
        // touching a real user. Nothing is written to it.
        var loader = new WindowsUserHiveLoader();
        string mountKey = "OTW-IntegrationTest-" + Guid.NewGuid().ToString("N");
        string systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
        string hivePath = Path.Combine(systemDrive + @"\", "Users", "Default", "NTUSER.DAT");
        Assert.True(File.Exists(hivePath), $"Default profile hive missing at {hivePath}");

        using var users =
            Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.Users, Microsoft.Win32.RegistryView.Registry64);

        using (var before = users.OpenSubKey(mountKey))
        {
            Assert.Null(before); // not mounted yet
        }

        loader.Load(mountKey, hivePath);
        try
        {
            using var mounted = users.OpenSubKey(mountKey);
            Assert.NotNull(mounted);
            Assert.NotEmpty(mounted.GetSubKeyNames()); // a real hive carries subkeys (Software, Environment, …)
        }
        finally
        {
            loader.Unload(mountKey);
        }

        using var after = users.OpenSubKey(mountKey);
        Assert.Null(after); // unloaded
    }

    [Fact]
    public void All_users_apply_writes_and_reverts_a_user_scope_value_in_the_current_hive()
    {
        RequireIntegration();

        // A real scope: AllUsers run through the production factory: the enumerator lists
        // the real users, and the current (logged-on) user's hive is written directly. It
        // exercises the enumerator + engine expansion + per-user-hive writer end to end.
        const string UserPath = @"Software\OpenTheWindows\IntegrationTestUser";
        string sid = WindowsIdentity.GetCurrent().User!.Value;
        string journalDir = Path.Combine(Path.GetTempPath(), "otw-int-" + Guid.NewGuid().ToString("N"));
        var reader = new WindowsRegistryReader();
        try
        {
            ApplyEngine engine = RealEngine(journalDir);
            TweakDefinition entry = UserRegistryEntry(UserPath, "Sample");

            ApplyResult applied = engine.Apply([entry], new ApplyOptions(
                WhatIf: false, CreateRestorePoint: false, BreakGlass: false, AllowAdvanced: true, AllowBreaking: true,
                "integration", RestartExplorer: false, Scope.AllUsers));
            Assert.Equal(RunState.Completed, applied.State);
            Assert.Equal(1, reader.Read(RegistryHive.User, sid, UserPath, "Sample").Data!.Value.GetInt32());

            RevertResult reverted = engine.Revert(applied.RunId, new RevertOptions(WhatIf: false, RestartExplorer: false));
            Assert.Equal(RunState.Completed, reverted.State);
            Assert.False(reader.Read(RegistryHive.User, sid, UserPath, "Sample").Exists);
        }
        finally
        {
            using var users =
                Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.Users, Microsoft.Win32.RegistryView.Registry64);
            using var owt = users.OpenSubKey(sid + @"\Software\OpenTheWindows", writable: true);
            owt?.DeleteSubKeyTree("IntegrationTestUser", throwOnMissingSubKey: false);
            if (Directory.Exists(journalDir))
            {
                Directory.Delete(journalDir, recursive: true);
            }
        }
    }

    [Fact]
    public void Active_setup_registrar_writes_bumps_and_removes_a_component()
    {
        RequireIntegration();

        // Writes a uniquely-named {OTW-…} component, confirms the values, bumps the version on
        // a second register, then removes it — the per-user logon fallback's real registry path.
        var registrar = new WindowsActiveSetupRegistrar();
        string id = "otw-integration-" + Guid.NewGuid().ToString("N");
        ActiveSetupComponent component = ActiveSetupFallback.For(id, "OTW Integration Test", @"C:\otw-test\otw.exe");
        string keyPath = ActiveSetupFallback.InstalledComponentsKey + @"\" + component.ComponentKey;
        using var hklm =
            Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64);
        try
        {
            ActiveSetupResult first = registrar.Register(component);
            Assert.Equal(ActiveSetupOutcome.Registered, first.Outcome);
            Assert.Equal("1", first.Version);

            using (var written = hklm.OpenSubKey(keyPath))
            {
                Assert.NotNull(written);
                Assert.Equal(component.StubPath, (string?)written.GetValue("StubPath"));
                Assert.Equal("OTW Integration Test", (string?)written.GetValue(null));
                Assert.Equal("1", (string?)written.GetValue("Version"));
            }

            // A second register bumps the version so Windows re-runs the stub at next sign-in.
            Assert.Equal("2", registrar.Register(component).Version);

            Assert.True(registrar.RemoveAll() >= 1);
            using var afterRemoval = hklm.OpenSubKey(keyPath);
            Assert.Null(afterRemoval);
        }
        finally
        {
            hklm.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void A_group_policy_managed_value_is_reported_and_left_untouched()
    {
        RequireIntegration();

        // Simulate an organisation GPO already governing a value: write it through the Local
        // GPO so it is recorded in Registry.pol (what the detector reads) and applied live,
        // then confirm scan reports it Managed and apply refuses to overwrite it — the product
        // never fights Group Policy (M5 acceptance: managed value reported and not overwritten).
        const string PolicyPath = @"SOFTWARE\Policies\OpenTheWindows\ManagedTest";
        string journalDir = Path.Combine(Path.GetTempPath(), "otw-int-" + Guid.NewGuid().ToString("N"));
        var reader = new WindowsRegistryReader();
        try
        {
            LocalGroupPolicy.Write(PolicyPath, "Sample", 0, Microsoft.Win32.RegistryValueKind.DWord);
            RunGpupdate();

            // Reported: the detector sees the value the Local GPO owns in Registry.pol, and the
            // live value the CSE applied is the policy's 0.
            Assert.Equal(
                ManagedState.GroupPolicy,
                new WindowsManagedSettingDetector().Detect(RegistryHive.LocalMachine, PolicyPath, "Sample"));
            Assert.Equal(0, reader.Read(RegistryHive.LocalMachine, null, PolicyPath, "Sample").Data!.Value.GetInt32());

            // Untouched: an entry that would set it to 1 is planned Managed and skipped, not applied.
            ApplyEngine engine = RealEngine(journalDir);
            TweakDefinition entry = RegistryEntry(PolicyPath, "Sample", RegistryValueKind.Policy);
            ApplyResult applied = engine.Apply([entry], new ApplyOptions(
                WhatIf: false, CreateRestorePoint: false, BreakGlass: false, AllowAdvanced: true, AllowBreaking: true, "integration", RestartExplorer: false));

            Assert.Equal(RunState.Completed, applied.State);
            Assert.Equal(ApplyState.Managed, Assert.Single(applied.Entries).Result);
            Assert.Equal(0, reader.Read(RegistryHive.LocalMachine, null, PolicyPath, "Sample").Data!.Value.GetInt32()); // still the GP value, not 1
        }
        finally
        {
            LocalGroupPolicy.Delete(PolicyPath, "Sample");
            RunGpupdate();
            using Microsoft.Win32.RegistryKey? policies = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\OpenTheWindows", writable: true);
            policies?.DeleteSubKeyTree("ManagedTest", throwOnMissingSubKey: false);
            if (Directory.Exists(journalDir))
            {
                Directory.Delete(journalDir, recursive: true);
            }
        }
    }

    [Fact]
    public void All_users_apply_reaches_a_logged_off_user_and_reverts_the_offline_hive()
    {
        RequireIntegration();

        // A genuinely offline user: a copy of the Default hive under a synthetic ProfileList SID
        // that is not mounted (no HKU\<sid>). The real enumerator lists it, so an all-users apply
        // must load it (RegLoadKey), write the user-scope value, and unload it — reaching a user
        // who is signed out — and revert must reload it to restore it (M5: "reaches a logged-off user").
        const string ProfileListKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList";
        const string UserPath = @"Software\OpenTheWindows\OfflineUserTest";
        const string Sid = "S-1-5-21-3141592653-2718281828-1618033988-4242";
        string systemDrive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
        string defaultHive = Path.Combine(systemDrive + @"\", "Users", "Default", "NTUSER.DAT");
        string profileDir = Path.Combine(systemDrive + @"\", "otw-test", "offline-" + Guid.NewGuid().ToString("N"));
        string journalDir = Path.Combine(Path.GetTempPath(), "otw-int-" + Guid.NewGuid().ToString("N"));
        string currentSid = WindowsIdentity.GetCurrent().User!.Value;
        var loader = new WindowsUserHiveLoader();

        RegisterOfflineProfile(ProfileListKey, Sid, profileDir, defaultHive);

        try
        {
            Assert.False(UsersSubKeyExists(Sid)); // not mounted before the run

            ApplyEngine engine = RealEngine(journalDir);
            ApplyResult applied = engine.Apply([UserRegistryEntry(UserPath, "Sample")], new ApplyOptions(
                WhatIf: false, CreateRestorePoint: false, BreakGlass: false, AllowAdvanced: true, AllowBreaking: true,
                "integration", RestartExplorer: false, Scope.AllUsers));
            Assert.Equal(RunState.Completed, applied.State);

            // The engine loaded the offline hive, wrote it, and unloaded it: the value is in the
            // file (read back under a fresh mount) and HKU\<sid> is not left mounted.
            Assert.Equal(1, ReadOfflineValue(loader, profileDir, "Sample"));
            Assert.False(UsersSubKeyExists(Sid)); // engine unloaded it

            // Revert must reload the now-offline hive and restore it to absent.
            RevertResult reverted = engine.Revert(applied.RunId, new RevertOptions(WhatIf: false, RestartExplorer: false));
            Assert.Equal(RunState.Completed, reverted.State);
            Assert.Null(ReadOfflineValue(loader, profileDir, "Sample"));
        }
        finally
        {
            RemoveOfflineProfile(ProfileListKey, Sid, currentSid, profileDir, journalDir);
        }
    }

    /// <summary>
    /// Registers a synthetic offline profile: a copy of the Default hive under
    /// <paramref name="profileDir"/> and a ProfileList entry for <paramref name="sid"/>
    /// pointing at it. The hive is not mounted, so the enumerator reports it offline.
    /// </summary>
    private static void RegisterOfflineProfile(string profileListKey, string sid, string profileDir, string defaultHive)
    {
        Directory.CreateDirectory(profileDir);
        File.Copy(defaultHive, Path.Combine(profileDir, "NTUSER.DAT"), overwrite: true);
        using Microsoft.Win32.RegistryKey list =
            Microsoft.Win32.Registry.LocalMachine.CreateSubKey(profileListKey + @"\" + sid, writable: true);
        list.SetValue("ProfileImagePath", profileDir, Microsoft.Win32.RegistryValueKind.ExpandString);
    }

    /// <summary>
    /// Removes the synthetic profile and the test's leftovers: the value the all-users run
    /// also wrote to the current admin's hive (revert restored it, this is defensive), the
    /// ProfileList entry, and the profile and journal directories.
    /// </summary>
    private static void RemoveOfflineProfile(string profileListKey, string sid, string currentSid, string profileDir, string journalDir)
    {
        using (var users = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.Users, Microsoft.Win32.RegistryView.Registry64))
        using (Microsoft.Win32.RegistryKey? owt = users.OpenSubKey(currentSid + @"\Software\OpenTheWindows", writable: true))
        {
            owt?.DeleteSubKeyTree("OfflineUserTest", throwOnMissingSubKey: false);
        }

        using (Microsoft.Win32.RegistryKey? list = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(profileListKey, writable: true))
        {
            list?.DeleteSubKeyTree(sid, throwOnMissingSubKey: false);
        }

        if (Directory.Exists(profileDir))
        {
            Directory.Delete(profileDir, recursive: true);
        }

        if (Directory.Exists(journalDir))
        {
            Directory.Delete(journalDir, recursive: true);
        }
    }

    /// <summary>Whether a subkey exists under HKEY_USERS (64-bit view) — i.e. whether that hive is mounted.</summary>
    private static bool UsersSubKeyExists(string subKey)
    {
        using var users =
            Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.Users, Microsoft.Win32.RegistryView.Registry64);
        using Microsoft.Win32.RegistryKey? key = users.OpenSubKey(subKey);
        return key is not null;
    }

    /// <summary>
    /// Loads the offline profile's hive under a throwaway mount, reads
    /// <c>Software\OpenTheWindows\OfflineUserTest\&lt;name&gt;</c>, and unloads. Returns the
    /// value as an <see cref="int"/>, or <see langword="null"/> when the value is absent.
    /// </summary>
    private static int? ReadOfflineValue(WindowsUserHiveLoader loader, string profileDir, string name)
    {
        string mount = "OTW-Verify-" + Guid.NewGuid().ToString("N");
        loader.Load(mount, Path.Combine(profileDir, "NTUSER.DAT"));
        try
        {
            using var users =
                Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.Users, Microsoft.Win32.RegistryView.Registry64);
            using Microsoft.Win32.RegistryKey? key = users.OpenSubKey(mount + @"\Software\OpenTheWindows\OfflineUserTest");
            return key?.GetValue(name) is int value ? value : null;
        }
        finally
        {
            loader.Unload(mount);
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

    private static TweakDefinition UserRegistryEntry(string path, string name)
    {
        var action = new RegistryAction(
            RegistryHive.User, path, name, RegistryValueType.Dword,
            JsonSerializer.SerializeToElement(1), JsonSerializer.SerializeToElement(0), RegistryValueKind.Preference);

        return new TweakDefinition(
            new TweakId("integration.registry.user"),
            "Integration user sample",
            "A per-user registry preference used only by the all-users integration test.",
            "Verifies an all-users apply/revert against a real user hive.",
            Category.Privacy,
            Level.Basic,
            RiskTier.Safe,
            Scope.User,
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
