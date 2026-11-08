using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.TestSupport;
using OpenTheWindows.Windows.Readers;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// Detects Group Policy ownership by reading a Local GPO <c>Registry.pol</c>.
/// Uses a temporary policy root built with <see cref="PRegFixture"/> so the
/// tests are deterministic and need no elevation, plus one real-path check that
/// an unmanaged value reports <see cref="ManagedState.NotManaged"/>.
/// </summary>
public sealed class WindowsManagedSettingDetectorTests : IDisposable
{
    private const string PolicyKey = @"Software\Policies\Otw";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "otw-gp-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Detects_a_policy_managed_value()
    {
        WriteMachinePolicy((PolicyKey, "Flag", 4u, [1, 0, 0, 0]));
        var detector = new WindowsManagedSettingDetector(_root);

        Assert.Equal(ManagedState.GroupPolicy, detector.Detect(RegistryHive.LocalMachine, PolicyKey, "Flag"));
    }

    [Fact]
    public void Detects_a_managed_value_deletion()
    {
        WriteMachinePolicy((PolicyKey, "**Del.Gone", 4u, [0, 0, 0, 0]));
        var detector = new WindowsManagedSettingDetector(_root);

        Assert.Equal(ManagedState.GroupPolicy, detector.Detect(RegistryHive.LocalMachine, PolicyKey, "Gone"));
    }

    [Fact]
    public void Value_or_key_not_in_policy_is_not_managed()
    {
        WriteMachinePolicy((PolicyKey, "Flag", 4u, [1, 0, 0, 0]));
        var detector = new WindowsManagedSettingDetector(_root);

        Assert.Equal(ManagedState.NotManaged, detector.Detect(RegistryHive.LocalMachine, PolicyKey, "Other"));
        Assert.Equal(ManagedState.NotManaged, detector.Detect(RegistryHive.LocalMachine, @"Software\Other", "Flag"));
    }

    [Fact]
    public void Missing_policy_file_is_not_managed()
    {
        var detector = new WindowsManagedSettingDetector(_root);

        Assert.Equal(ManagedState.NotManaged, detector.Detect(RegistryHive.LocalMachine, PolicyKey, "Flag"));
    }

    [Fact]
    public void Unmanaged_machine_reports_not_managed()
    {
        var detector = new WindowsManagedSettingDetector();

        ManagedState state = detector.Detect(
            RegistryHive.LocalMachine, @"Software\Policies\OpenTheWindows\NotReal", "NotReal");

        Assert.Equal(ManagedState.NotManaged, state);
    }

    private void WriteMachinePolicy(params (string Key, string Value, uint Type, byte[] Data)[] entries)
    {
        string machineDir = Path.Combine(_root, "Machine");
        Directory.CreateDirectory(machineDir);
        File.WriteAllBytes(Path.Combine(machineDir, "Registry.pol"), PRegFixture.Build(entries));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private Windows.Security.PolicyOwnershipLedger Ledger() => new(Path.Combine(_root, "state"), guard: null);

    private static Core.Journal.JournalRecord AppliedPolicyRun(Guid runId, string name, int value, Guid? revertOf = null) => new()
    {
        RunId = runId,
        StartedAt = DateTimeOffset.UnixEpoch,
        AppVersion = "0.0.0",
        Os = new Core.Journal.JournalOs(26200, 0, "Professional"),
        Profile = "test",
        Operator = "test",
        RestorePoint = new Core.Journal.JournalRestorePoint(false, false, "NotRequested", null),
        Result = Core.Journal.RunState.Completed,
        RevertOf = revertOf,
        Entries =
        [
            new Core.Journal.JournalEntry
            {
                Id = "test.policy",
                Result = Core.Journal.ApplyState.Applied,
                Actions =
                [
                    new Core.Journal.JournalAction
                    {
                        Kind = "Registry",
                        Target = name,
                        Action = new Core.Catalog.Actions.RegistryAction(
                            RegistryHive.LocalMachine, PolicyKey, name, RegistryValueType.Dword,
                            System.Text.Json.JsonSerializer.SerializeToElement(value), System.Text.Json.JsonSerializer.SerializeToElement(0),
                            RegistryValueKind.Policy),
                        Desired = new Core.Journal.JournalActionState(),
                        Result = Core.Journal.ApplyState.Applied,
                    },
                ],
            },
        ],
    };

    private static TestSupport.Fakes.FakeMachine LiveFlag(int value)
    {
        var machine = new TestSupport.Fakes.FakeMachine();
        machine.SetRegistry(RegistryHive.LocalMachine, null, PolicyKey, "Flag", RegistryValueType.Dword, System.Text.Json.JsonSerializer.SerializeToElement(value));
        return machine;
    }

    [Fact]
    public void A_local_policy_value_this_product_wrote_is_not_treated_as_managed()
    {
        WriteMachinePolicy((PolicyKey, "Flag", 4u, [1, 0, 0, 0]));
        Windows.Security.PolicyOwnershipLedger ledger = Ledger();
        ledger.Record(AppliedPolicyRun(Guid.NewGuid(), "Flag", 1));

        var detector = new WindowsManagedSettingDetector(_root, ledger, LiveFlag(1), static () => null);

        Assert.Equal(ManagedState.NotManaged, detector.Detect(RegistryHive.LocalMachine, PolicyKey, "Flag"));
    }

    [Fact]
    public void A_local_policy_value_an_administrator_changed_afterwards_is_managed_again()
    {
        WriteMachinePolicy((PolicyKey, "Flag", 4u, [2, 0, 0, 0]));
        Windows.Security.PolicyOwnershipLedger ledger = Ledger();
        ledger.Record(AppliedPolicyRun(Guid.NewGuid(), "Flag", 1));

        var detector = new WindowsManagedSettingDetector(_root, ledger, LiveFlag(2), static () => null);

        Assert.Equal(ManagedState.GroupPolicy, detector.Detect(RegistryHive.LocalMachine, PolicyKey, "Flag"));
    }

    [Fact]
    public void Reverting_the_run_gives_up_ownership()
    {
        WriteMachinePolicy((PolicyKey, "Flag", 4u, [1, 0, 0, 0]));
        Windows.Security.PolicyOwnershipLedger ledger = Ledger();
        var applied = Guid.NewGuid();
        ledger.Record(AppliedPolicyRun(applied, "Flag", 1));
        ledger.Record(AppliedPolicyRun(Guid.NewGuid(), "Flag", 0, revertOf: applied));

        Assert.False(ledger.IsOwned(PolicyKey, "Flag", System.Text.Json.JsonSerializer.SerializeToElement(1)));
    }

    [Fact]
    public void On_an_organisation_managed_device_a_live_policy_value_from_elsewhere_is_managed()
    {
        var detector = new WindowsManagedSettingDetector(_root, Ledger(), LiveFlag(1), static () => ManagedState.Mdm);

        Assert.Equal(ManagedState.Mdm, detector.Detect(RegistryHive.LocalMachine, PolicyKey, "Flag"));
        Assert.Equal(ManagedState.NotManaged, detector.Detect(RegistryHive.LocalMachine, PolicyKey, "Absent"));
    }

    [Fact]
    public void On_an_unmanaged_device_a_live_policy_value_from_another_tool_is_not_managed()
    {
        var detector = new WindowsManagedSettingDetector(_root, Ledger(), LiveFlag(1), static () => null);

        Assert.Equal(ManagedState.NotManaged, detector.Detect(RegistryHive.LocalMachine, PolicyKey, "Flag"));
    }

    [Fact]
    public void An_unreadable_ledger_owns_nothing()
    {
        Directory.CreateDirectory(Path.Combine(_root, "state"));
        File.WriteAllText(Path.Combine(_root, "state", "owned-policies.json"), "{ not json");

        Assert.False(Ledger().IsOwned(PolicyKey, "Flag", System.Text.Json.JsonSerializer.SerializeToElement(1)));
    }
}
