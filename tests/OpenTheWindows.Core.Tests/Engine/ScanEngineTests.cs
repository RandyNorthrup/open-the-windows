using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;
using OpenTheWindows.TestSupport.Fakes;
using static OpenTheWindows.Core.Tests.Catalog.CatalogTestData;

namespace OpenTheWindows.Core.Tests.Engine;

public sealed class ScanEngineTests
{
    private static readonly DateTimeOffset FixedInstant = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    private static ScanEngine Build(FakeReaders readers, OperatingSystemFacts? os = null)
        => new(readers, readers, readers, readers, readers, readers, readers, readers, readers, readers,
            new FakeOperatingSystemInfo(os ?? FakeOperatingSystemInfo.Windows11Pro24H2()),
            new FixedTimeProvider(FixedInstant));

    private static TweakObservation ScanOne(FakeReaders readers, TweakDefinition entry, OperatingSystemFacts? os = null)
        => Build(readers, os).Scan([entry], "test").Results[0];

    private static TweakDefinition WithActions(params ITweakAction[] actions)
        => ValidEntry("scan.test.entry") with { Actions = actions };

    private static RegistryAction Registry(RegistryValueType type, JsonElement value, RegistryHive hive = RegistryHive.LocalMachine)
        => new(hive, @"SOFTWARE\Test\Key", "Name", type, value, Number(0), RegistryValueKind.Preference);

    // ---- Registry ---------------------------------------------------------

    [Fact]
    public void Registry_matching_value_is_compliant()
    {
        var readers = new FakeReaders { OnRegistry = (_, _, _, _) => new RegistryValueSnapshot(true, RegistryValueType.Dword, Number(1)) };

        TweakObservation result = ScanOne(readers, WithActions(Registry(RegistryValueType.Dword, Number(1))));

        Assert.Equal(ComplianceState.Compliant, result.State);
    }

    [Fact]
    public void Registry_different_value_is_drift()
    {
        var readers = new FakeReaders { OnRegistry = (_, _, _, _) => new RegistryValueSnapshot(true, RegistryValueType.Dword, Number(0)) };

        TweakObservation result = ScanOne(readers, WithActions(Registry(RegistryValueType.Dword, Number(1))));

        Assert.Equal(ComplianceState.Drift, result.State);
    }

    [Fact]
    public void Registry_absent_value_is_drift()
    {
        var readers = new FakeReaders(); // default: absent

        TweakObservation result = ScanOne(readers, WithActions(Registry(RegistryValueType.Dword, Number(1))));

        Assert.Equal(ComplianceState.Drift, result.State);
        Assert.Equal("(absent)", result.Actions[0].Actual);
    }

    [Fact]
    public void Registry_wrong_type_is_drift()
    {
        var readers = new FakeReaders { OnRegistry = (_, _, _, _) => new RegistryValueSnapshot(true, RegistryValueType.Sz, Text("1")) };

        TweakObservation result = ScanOne(readers, WithActions(Registry(RegistryValueType.Dword, Number(1))));

        Assert.Equal(ComplianceState.Drift, result.State);
    }

    [Fact]
    public void Registry_managed_setting_is_managed_even_when_value_matches()
    {
        var readers = new FakeReaders
        {
            OnRegistry = (_, _, _, _) => new RegistryValueSnapshot(true, RegistryValueType.Dword, Number(1)),
            OnManaged = (_, _, _) => ManagedState.GroupPolicy,
        };

        TweakObservation result = ScanOne(readers, WithActions(Registry(RegistryValueType.Dword, Number(1))));

        Assert.Equal(ComplianceState.Managed, result.State);
        Assert.Equal(ManagedState.GroupPolicy, result.Actions[0].Managed);
    }

    [Fact]
    public void Registry_managed_setting_not_at_the_desired_value_is_drift_not_compliant()
    {
        // Policy-owned (present in Registry.pol) but the live value does not match the desired data —
        // the inconsistent state a policy write whose revert did not complete leaves behind. Both the
        // absent case and the different-value case must read as drift, never silently "managed and
        // therefore fine", while still recording that Group Policy owns the value.
        AssertManagedButNotDesiredIsDrift(new RegistryValueSnapshot(false, null, null), "(absent)");
        AssertManagedButNotDesiredIsDrift(new RegistryValueSnapshot(true, RegistryValueType.Dword, Number(0)), expectedActual: null);
    }

    private static void AssertManagedButNotDesiredIsDrift(RegistryValueSnapshot live, string? expectedActual)
    {
        var readers = new FakeReaders
        {
            OnRegistry = (_, _, _, _) => live,
            OnManaged = (_, _, _) => ManagedState.GroupPolicy,
        };

        TweakObservation result = ScanOne(readers, WithActions(Registry(RegistryValueType.Dword, Number(1))));

        Assert.Equal(ComplianceState.Drift, result.State);
        Assert.Equal(ManagedState.GroupPolicy, result.Actions[0].Managed);
        if (expectedActual is not null)
        {
            Assert.Equal(expectedActual, result.Actions[0].Actual);
        }
    }

    [Fact]
    public void Registry_multi_sz_sequence_is_compared()
    {
        JsonElement desired = JsonSerializer.SerializeToElement(new[] { "a", "b" });
        var compliant = new FakeReaders { OnRegistry = (_, _, _, _) => new RegistryValueSnapshot(true, RegistryValueType.MultiSz, JsonSerializer.SerializeToElement(new[] { "a", "b" })) };
        var drifted = new FakeReaders { OnRegistry = (_, _, _, _) => new RegistryValueSnapshot(true, RegistryValueType.MultiSz, JsonSerializer.SerializeToElement(new[] { "a", "c" })) };

        Assert.Equal(ComplianceState.Compliant, ScanOne(compliant, WithActions(Registry(RegistryValueType.MultiSz, desired))).State);
        Assert.Equal(ComplianceState.Drift, ScanOne(drifted, WithActions(Registry(RegistryValueType.MultiSz, desired))).State);
    }

    // ---- SecurityPolicy (secedit) -----------------------------------------

    private static SecurityPolicyAction Policy(string value)
        => new(SecurityPolicySection.SystemAccess, "MinimumPasswordLength", value, "0");

    [Fact]
    public void SecurityPolicy_matching_value_is_compliant()
    {
        var readers = new FakeReaders { OnSecurityPolicy = (_, _) => "14" };

        Assert.Equal(ComplianceState.Compliant, ScanOne(readers, WithActions(Policy("14"))).State);
    }

    [Fact]
    public void SecurityPolicy_absent_or_different_value_is_drift()
    {
        var absent = new FakeReaders();
        var different = new FakeReaders { OnSecurityPolicy = (_, _) => "8" };

        Assert.Equal(ComplianceState.Drift, ScanOne(absent, WithActions(Policy("14"))).State);
        Assert.Equal("(absent)", ScanOne(absent, WithActions(Policy("14"))).Actions[0].Actual);
        Assert.Equal(ComplianceState.Drift, ScanOne(different, WithActions(Policy("14"))).State);
    }

    [Fact]
    public void Registry_user_hive_read_receives_the_interactive_user_sid()
    {
        string? capturedSid = "unset";
        FakeReaders readers = SidCapturingReaders(sid => capturedSid = sid);
        TweakDefinition entry = WithActions(Registry(RegistryValueType.Dword, Number(1), RegistryHive.User)) with { Scope = Scope.User };

        ScanOne(readers, entry);

        Assert.Equal("S-1-5-21-1-2-3-1001", capturedSid);
    }

    [Fact]
    public void Registry_machine_hive_read_receives_no_sid()
    {
        string? capturedSid = "unset";
        FakeReaders readers = SidCapturingReaders(sid => capturedSid = sid);

        ScanOne(readers, WithActions(Registry(RegistryValueType.Dword, Number(1))));

        Assert.Null(capturedSid);
    }

    private static FakeReaders SidCapturingReaders(Action<string?> onSid) => new()
    {
        OnInteractiveUser = () => new InteractiveUser("S-1-5-21-1-2-3-1001", @"PC\user", HiveLoaded: true),
        OnRegistry = (_, sid, _, _) =>
        {
            onSid(sid);
            return new RegistryValueSnapshot(true, RegistryValueType.Dword, Number(1));
        },
    };

    // ---- Service ----------------------------------------------------------

    [Fact]
    public void Service_matching_start_type_is_compliant()
    {
        var readers = new FakeReaders { OnService = name => new ServiceSnapshot(name, ServiceStartType.Disabled, false, false) };

        TweakObservation result = ScanOne(readers, WithActions(new ServiceAction("DiagTrack", ServiceStartType.Disabled, ServiceStartType.Automatic, true)));

        Assert.Equal(ComplianceState.Compliant, result.State);
    }

    [Fact]
    public void Service_different_start_type_is_drift()
    {
        var readers = new FakeReaders { OnService = name => new ServiceSnapshot(name, ServiceStartType.Automatic, true, false) };

        TweakObservation result = ScanOne(readers, WithActions(new ServiceAction("DiagTrack", ServiceStartType.Disabled, ServiceStartType.Automatic, true)));

        Assert.Equal(ComplianceState.Drift, result.State);
    }

    [Fact]
    public void Service_absent_is_not_applicable()
    {
        var readers = new FakeReaders(); // default: null service

        TweakObservation result = ScanOne(readers, WithActions(new ServiceAction("Missing", ServiceStartType.Disabled, ServiceStartType.Automatic, true)));

        Assert.Equal(ComplianceState.NotApplicable, result.State);
    }

    // ---- Scheduled task ---------------------------------------------------

    [Fact]
    public void Task_matching_state_is_compliant()
    {
        var readers = new FakeReaders { OnTask = path => new ScheduledTaskSnapshot(path, false, true) };

        TweakObservation result = ScanOne(readers, WithActions(new ScheduledTaskAction(@"\Microsoft\Windows\Foo", false, true)));

        Assert.Equal(ComplianceState.Compliant, result.State);
    }

    [Fact]
    public void Task_different_state_is_drift()
    {
        var readers = new FakeReaders { OnTask = path => new ScheduledTaskSnapshot(path, true, true) };

        TweakObservation result = ScanOne(readers, WithActions(new ScheduledTaskAction(@"\Microsoft\Windows\Foo", false, true)));

        Assert.Equal(ComplianceState.Drift, result.State);
    }

    [Fact]
    public void Task_absent_is_not_applicable()
    {
        var readers = new FakeReaders { OnTask = path => new ScheduledTaskSnapshot(path, false, false) };

        TweakObservation result = ScanOne(readers, WithActions(new ScheduledTaskAction(@"\Microsoft\Windows\Foo", false, true)));

        Assert.Equal(ComplianceState.NotApplicable, result.State);
    }

    // ---- Appx -------------------------------------------------------------

    [Fact]
    public void Appx_not_installed_for_user_is_compliant()
    {
        var readers = new FakeReaders(); // default: not installed anywhere

        TweakObservation result = ScanOne(readers, WithActions(new AppxAction("Contoso.App_abc", AppxRemovalMode.CurrentUser, "Store")));

        Assert.Equal(ComplianceState.Compliant, result.State);
    }

    [Fact]
    public void Appx_installed_for_user_is_drift()
    {
        var readers = new FakeReaders { OnAppx = (_, _) => new AppxSnapshot(true, true, false, false) };

        TweakObservation result = ScanOne(readers, WithActions(new AppxAction("Contoso.App_abc", AppxRemovalMode.CurrentUser, "Store")));

        Assert.Equal(ComplianceState.Drift, result.State);
    }

    [Fact]
    public void Appx_all_users_requires_deprovisioned_to_be_compliant()
    {
        var notDeprovisioned = new FakeReaders { OnAppx = (_, _) => new AppxSnapshot(false, false, true, false) };
        var deprovisioned = new FakeReaders { OnAppx = (_, _) => new AppxSnapshot(false, false, false, true) };
        AppxAction action = new("Contoso.App_abc", AppxRemovalMode.AllUsersAndDeprovision, "Store");

        Assert.Equal(ComplianceState.Drift, ScanOne(notDeprovisioned, WithActions(action)).State);
        Assert.Equal(ComplianceState.Compliant, ScanOne(deprovisioned, WithActions(action)).State);
    }

    // ---- Optional feature -------------------------------------------------

    [Fact]
    public void Feature_matching_state_is_compliant()
    {
        var readers = new FakeReaders { OnFeature = _ => false };

        TweakObservation result = ScanOne(readers, WithActions(new OptionalFeatureAction("SMB1Protocol", false, true)));

        Assert.Equal(ComplianceState.Compliant, result.State);
    }

    [Fact]
    public void Feature_absent_is_not_applicable()
    {
        var readers = new FakeReaders(); // default: null

        TweakObservation result = ScanOne(readers, WithActions(new OptionalFeatureAction("SMB1Protocol", false, true)));

        Assert.Equal(ComplianceState.NotApplicable, result.State);
    }

    // ---- Defender ---------------------------------------------------------

    [Fact]
    public void Defender_matching_value_is_compliant()
    {
        var readers = new FakeReaders { OnDefender = _ => Number(1) };

        TweakObservation result = ScanOne(readers, WithActions(new DefenderPreferenceAction("PUAProtection", Number(1), Number(0))));

        Assert.Equal(ComplianceState.Compliant, result.State);
    }

    [Fact]
    public void Defender_unreadable_is_unknown()
    {
        var readers = new FakeReaders(); // default: null

        TweakObservation result = ScanOne(readers, WithActions(new DefenderPreferenceAction("PUAProtection", Number(1), Number(0))));

        Assert.Equal(ComplianceState.Unknown, result.State);
    }

    // ---- Power ------------------------------------------------------------

    [Fact]
    public void Power_matching_values_is_compliant()
    {
        var readers = new FakeReaders { OnPower = (_, _) => (10u, 5u) };

        TweakObservation result = ScanOne(readers, WithActions(new PowerSettingAction(Guid.Empty, Guid.Empty, 10u, 5u)));

        Assert.Equal(ComplianceState.Compliant, result.State);
    }

    [Fact]
    public void Power_absent_is_not_applicable()
    {
        var readers = new FakeReaders(); // default: null

        TweakObservation result = ScanOne(readers, WithActions(new PowerSettingAction(Guid.Empty, Guid.Empty, 10u, 5u)));

        Assert.Equal(ComplianceState.NotApplicable, result.State);
    }

    // ---- Registry value types & drift paths -------------------------------

    [Fact]
    public void Registry_qword_matching_is_compliant_and_mismatch_is_drift()
    {
        var match = new FakeReaders { OnRegistry = (_, _, _, _) => new RegistryValueSnapshot(true, RegistryValueType.Qword, Number(42)) };
        var mismatch = new FakeReaders { OnRegistry = (_, _, _, _) => new RegistryValueSnapshot(true, RegistryValueType.Qword, Number(7)) };

        Assert.Equal(ComplianceState.Compliant, ScanOne(match, WithActions(Registry(RegistryValueType.Qword, Number(42)))).State);
        Assert.Equal(ComplianceState.Drift, ScanOne(mismatch, WithActions(Registry(RegistryValueType.Qword, Number(42)))).State);
    }

    [Fact]
    public void Registry_sz_matching_is_compliant_and_mismatch_is_drift()
    {
        var match = new FakeReaders { OnRegistry = (_, _, _, _) => new RegistryValueSnapshot(true, RegistryValueType.Sz, Text("keep")) };
        var mismatch = new FakeReaders { OnRegistry = (_, _, _, _) => new RegistryValueSnapshot(true, RegistryValueType.Sz, Text("other")) };

        Assert.Equal(ComplianceState.Compliant, ScanOne(match, WithActions(Registry(RegistryValueType.Sz, Text("keep")))).State);
        Assert.Equal(ComplianceState.Drift, ScanOne(mismatch, WithActions(Registry(RegistryValueType.Sz, Text("keep")))).State);
    }

    [Fact]
    public void Registry_binary_matching_is_compliant_and_mismatch_is_drift()
    {
        var match = new FakeReaders { OnRegistry = (_, _, _, _) => new RegistryValueSnapshot(true, RegistryValueType.Binary, Text("AQID")) };
        var mismatch = new FakeReaders { OnRegistry = (_, _, _, _) => new RegistryValueSnapshot(true, RegistryValueType.Binary, Text("AQIE")) };

        Assert.Equal(ComplianceState.Compliant, ScanOne(match, WithActions(Registry(RegistryValueType.Binary, Text("AQID")))).State);
        Assert.Equal(ComplianceState.Drift, ScanOne(mismatch, WithActions(Registry(RegistryValueType.Binary, Text("AQID")))).State);
    }

    [Fact]
    public void Feature_enabled_when_it_should_be_disabled_is_drift()
    {
        var readers = new FakeReaders { OnFeature = _ => true };

        TweakObservation result = ScanOne(readers, WithActions(new OptionalFeatureAction("SMB1Protocol", false, true)));

        Assert.Equal(ComplianceState.Drift, result.State);
    }

    [Fact]
    public void Power_different_values_is_drift()
    {
        var readers = new FakeReaders { OnPower = (_, _) => (1u, 2u) };

        TweakObservation result = ScanOne(readers, WithActions(new PowerSettingAction(Guid.Empty, Guid.Empty, 10u, 5u)));

        Assert.Equal(ComplianceState.Drift, result.State);
    }

    [Fact]
    public void Defender_different_value_is_drift()
    {
        var readers = new FakeReaders { OnDefender = _ => Number(2) };

        TweakObservation result = ScanOne(readers, WithActions(new DefenderPreferenceAction("PUAProtection", Number(1), Number(0))));

        Assert.Equal(ComplianceState.Drift, result.State);
    }

    // ---- Command ----------------------------------------------------------

    [Fact]
    public void Command_is_always_unknown()
    {
        var readers = new FakeReaders();

        TweakObservation result = ScanOne(readers, WithActions(new CommandAction("powercfg.exe", ["/x"], ["/y"])));

        Assert.Equal(ComplianceState.Unknown, result.State);
    }

    // ---- Aggregation and applicability ------------------------------------

    [Fact]
    public void Entry_state_is_the_worst_of_its_actions()
    {
        var readers = new FakeReaders
        {
            OnRegistry = (_, _, _, _) => new RegistryValueSnapshot(true, RegistryValueType.Dword, Number(1)), // compliant
            OnService = name => new ServiceSnapshot(name, ServiceStartType.Automatic, true, false),           // drift
        };
        TweakDefinition entry = WithActions(
            Registry(RegistryValueType.Dword, Number(1)),
            new ServiceAction("DiagTrack", ServiceStartType.Disabled, ServiceStartType.Automatic, true));

        Assert.Equal(ComplianceState.Drift, ScanOne(readers, entry).State);
    }

    [Fact]
    public void Not_applicable_entry_is_reported_without_reading()
    {
        var readers = new FakeReaders
        {
            OnRegistry = (_, _, _, _) => throw new InvalidOperationException("readers must not be called for a non-applicable entry"),
        };
        TweakDefinition entry = WithActions(Registry(RegistryValueType.Dword, Number(1)))
            with
        { AppliesTo = new Applicability([], 27000, null, []) }; // min build above the fake OS (26100)

        TweakObservation result = ScanOne(readers, entry);

        Assert.Equal(ComplianceState.NotApplicable, result.State);
        Assert.Empty(result.Actions);
    }

    [Fact]
    public void Report_is_stamped_with_the_time_provider_and_profile()
    {
        var readers = new FakeReaders();

        ScanReport report = Build(readers).Scan([WithActions(new CommandAction("powercfg.exe", ["/x"], ["/y"]))], "balanced");

        Assert.Equal(FixedInstant, report.At);
        Assert.Equal("balanced", report.ProfileName);
        Assert.Equal(26100, report.Os.Build);
    }

    [Fact]
    public void Report_counts_reflect_states()
    {
        var readers = new FakeReaders { OnRegistry = (_, _, _, _) => new RegistryValueSnapshot(true, RegistryValueType.Dword, Number(0)) };
        TweakDefinition drift = WithActions(Registry(RegistryValueType.Dword, Number(1)));

        ScanReport report = Build(readers).Scan([drift], "test");

        Assert.Equal(1, report.DriftCount);
        Assert.Equal(0, report.CompliantCount);
        Assert.False(report.IsCompliant);
    }
}
