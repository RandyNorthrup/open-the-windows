using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Tests.Catalog;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Core.Tests.Engine;

/// <summary>
/// Direct per-kind tests for the action applier: capturing prior state
/// (present and absent), verifying applied and restored state, and describing
/// desired state — exercising each action kind's branches without the engine.
/// </summary>
public sealed class ActionApplierTests
{
    private const string Path = @"SOFTWARE\OpenTheWindows\ApplierTest";
    private static readonly Guid Sub = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Set = new("22222222-2222-2222-2222-222222222222");

    private static (ActionApplier Applier, FakeMachine Machine) New()
    {
        var m = new FakeMachine();
        var applier = new ActionApplier(
            new StateReaders(m, m, m, m, m, m, m),
            new StateWriters(m, m, m, m, m, m, m, m));
        return (applier, m);
    }

    private static JsonElement Num(int value) => JsonSerializer.SerializeToElement(value);

    private static RegistryAction Reg() => new(RegistryHive.LocalMachine, Path, "V", RegistryValueType.Dword, Num(1), Num(0), RegistryValueKind.Preference);
    private static ServiceAction Svc(string name = "Svc") => new(name, ServiceStartType.Disabled, ServiceStartType.Automatic, false);
    private static ScheduledTaskAction Task(string path = @"\T") => new(path, false, true);
    private static OptionalFeatureAction Feature(string name = "F") => new(name, false, true);
    private static DefenderPreferenceAction Defender() => new("PUAProtection", Num(1), Num(0));
    private static PowerSettingAction Power() => new(Sub, Set, 5, 5);
    private static AppxAction Appx() => new("Pkg_abc", AppxRemovalMode.AllUsersAndDeprovision, "Store");
    private static CommandAction Command() => new("powercfg.exe", ["/a"], ["/b"]);

    [Fact]
    public void CapturePrior_reports_absent_for_missing_targets()
    {
        (ActionApplier applier, _) = New();

        Assert.False(applier.CapturePrior(Reg(), null)!.Exists);
        Assert.False(applier.CapturePrior(Svc(), null)!.Exists);
        Assert.False(applier.CapturePrior(Task(), null)!.Exists);
        Assert.False(applier.CapturePrior(Feature(), null)!.Exists);
        Assert.False(applier.CapturePrior(Defender(), null)!.Exists);
        Assert.False(applier.CapturePrior(Power(), null)!.Exists);
        Assert.Null(applier.CapturePrior(Command(), null));
    }

    [Fact]
    public void CapturePrior_captures_present_state()
    {
        (ActionApplier applier, FakeMachine machine) = New();
        machine.SetService(new ServiceSnapshot("Svc", ServiceStartType.Automatic, IsRunning: true, IsProtectedProcess: false));
        machine.SetTask(new ScheduledTaskSnapshot(@"\T", Enabled: true, Exists: true));
        machine.SetFeature("F", enabled: true);
        machine.SetPower(Sub, Set, 3, 4);

        JournalActionState service = applier.CapturePrior(Svc(), null)!;
        Assert.True(service.Exists);
        Assert.Equal(ServiceStartType.Automatic, service.StartType);
        Assert.True(service.Running);

        Assert.True(applier.CapturePrior(Task(), null)!.Enabled);
        Assert.True(applier.CapturePrior(Feature(), null)!.Enabled);

        JournalActionState power = applier.CapturePrior(Power(), null)!;
        Assert.Equal(3u, power.Ac);
        Assert.Equal(4u, power.Dc);
    }

    [Fact]
    public void DescribeDesired_covers_every_kind()
    {
        Assert.Equal(RegistryValueType.Dword, ActionApplier.DescribeDesired(Reg()).Type);
        Assert.Equal(ServiceStartType.Disabled, ActionApplier.DescribeDesired(Svc()).StartType);
        Assert.False(ActionApplier.DescribeDesired(Task()).Enabled);
        Assert.False(ActionApplier.DescribeDesired(Feature()).Enabled);
        Assert.True(ActionApplier.DescribeDesired(Appx()).Deprovisioned);
        Assert.NotNull(ActionApplier.DescribeDesired(Defender()).Data);
        Assert.Equal(5u, ActionApplier.DescribeDesired(Power()).Ac);
        Assert.Null(ActionApplier.DescribeDesired(Command()).Type);
    }

    [Fact]
    public void Verify_is_false_for_absent_targets()
    {
        (ActionApplier applier, _) = New();

        Assert.False(applier.Verify(Reg(), null));
        Assert.False(applier.Verify(Svc(), null));
        Assert.False(applier.Verify(Task(), null));
        Assert.True(applier.Verify(Command(), null)); // commands cannot be verified; treated as applied
    }

    [Fact]
    public void VerifyRestored_is_true_when_the_prior_field_is_absent()
    {
        (ActionApplier applier, _) = New();
        var empty = new JournalActionState();

        Assert.True(applier.VerifyRestored(Svc(), empty, null));
        Assert.True(applier.VerifyRestored(Task(), empty, null));
        Assert.True(applier.VerifyRestored(Feature(), empty, null));
        Assert.True(applier.VerifyRestored(Defender(), empty, null));
        Assert.True(applier.VerifyRestored(Power(), empty, null));
        Assert.True(applier.VerifyRestored(Appx(), empty, null));
        Assert.True(applier.VerifyRestored(Command(), empty, null));
        Assert.True(applier.VerifyRestored(Reg(), new JournalActionState { Exists = false }, null)); // absent value stays absent
    }

    [Fact]
    public void Restore_is_a_no_op_when_the_prior_is_absent()
    {
        (ActionApplier applier, FakeMachine machine) = New();
        var empty = new JournalActionState();

        applier.Restore(Svc(), empty, null);
        applier.Restore(Task(), new JournalActionState { Exists = false }, null);
        applier.Restore(Feature(), new JournalActionState { Exists = false }, null);

        Assert.Equal(0, machine.WriteCount); // nothing was written
    }

    [Fact]
    public void Apply_service_sets_the_start_type_and_stops_a_running_service()
    {
        (ActionApplier applier, FakeMachine machine) = New();
        machine.SetService(new ServiceSnapshot("Svc", ServiceStartType.Automatic, IsRunning: true, IsProtectedProcess: false));

        applier.Apply(new ServiceAction("Svc", ServiceStartType.Disabled, ServiceStartType.Automatic, StopIfRunning: true), null);

        ServiceSnapshot after = ((IServiceReader)machine).Read("Svc")!;
        Assert.Equal(ServiceStartType.Disabled, after.StartType);
        Assert.False(after.IsRunning);
    }

    [Fact]
    public void A_failing_command_throws()
    {
        (ActionApplier applier, FakeMachine machine) = New();
        machine.CommandExitCode = 1;

        Assert.Throws<RefusedOperationException>(() => applier.Apply(Command(), null));
    }

    [Fact]
    public void A_protected_process_service_is_refused_at_plan_time()
    {
        (ActionApplier applier, FakeMachine machine) = New();
        machine.SetService(new ServiceSnapshot("Ppl", ServiceStartType.Automatic, IsRunning: true, IsProtectedProcess: true));

        Assert.NotNull(applier.CheckRefusal(new ServiceAction("Ppl", ServiceStartType.Disabled, ServiceStartType.Automatic, false)));
    }

    [Fact]
    public void IsUserScoped_action_is_true_only_for_a_user_hive_or_appx_action()
    {
        Assert.True(ActionApplier.IsUserScoped(UserReg()));
        Assert.True(ActionApplier.IsUserScoped(Appx()));
        Assert.False(ActionApplier.IsUserScoped(Reg()));    // HKLM registry value
        Assert.False(ActionApplier.IsUserScoped(Svc()));
        Assert.False(ActionApplier.IsUserScoped(Task()));
    }

    [Fact]
    public void IsUserScoped_entry_requires_every_action_to_be_user_scoped()
    {
        // The predicate a --user-scope remediation filters on: an entry only qualifies when
        // it writes nothing outside the user's own hive, so a non-elevated run is safe.
        Assert.True(ActionApplier.IsUserScoped(CatalogTestData.ValidEntry("u") with { Actions = [UserReg(), Appx()] }));
        Assert.False(ActionApplier.IsUserScoped(CatalogTestData.ValidEntry("mixed") with { Actions = [UserReg(), Reg()] }));
        Assert.False(ActionApplier.IsUserScoped(CatalogTestData.ValidEntry("machine") with { Actions = [Reg()] }));
        Assert.False(ActionApplier.IsUserScoped(CatalogTestData.ValidEntry("empty") with { Actions = [] }));
    }

    private static RegistryAction UserReg() => new(RegistryHive.User, Path, "V", RegistryValueType.Dword, Num(1), Num(0), RegistryValueKind.Preference);
}
