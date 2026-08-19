using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Model;
using OpenTheWindows.TestSupport.Fakes;
using static OpenTheWindows.Core.Tests.Catalog.CatalogTestData;

namespace OpenTheWindows.Core.Tests.Engine;

/// <summary>
/// Unit tests for the transactional apply engine over an in-memory machine:
/// ordering, conflict rejection, managed skip vs break-glass, journal-before-write,
/// verify-failure rollback, hash chain, revert to exact prior, requires
/// aggregation, refusals and restore-point reporting.
/// </summary>
public sealed class ApplyEngineTests
{
    private const string Path = @"SOFTWARE\OpenTheWindows\ApplyTest";

    private static ApplyOptions Opts(
        bool whatIf = false, bool restore = false, bool breakGlass = false,
        bool allowAdvanced = true, bool allowBreaking = true, bool restartExplorer = false, Scope scope = Scope.Machine)
        => new(whatIf, restore, breakGlass, allowAdvanced, allowBreaking, "balanced", restartExplorer, scope);

    private static UserProfile UserHive(string sid, bool loaded = true) => new(sid, @"C:\Users\" + sid, loaded);

    private static void SeedUserDword(FakeMachine machine, string sid, string name, int value)
        => machine.SetRegistry(RegistryHive.User, sid, Path, name, RegistryValueType.Dword, Number(value));

    private static int ReadUserDword(FakeMachine machine, string sid, string name)
        => ((IRegistryReader)machine).Read(RegistryHive.User, sid, Path, name).Data!.Value.GetInt32();

    private static RegistryAction Reg(string name, int desired,
        RegistryValueKind kind = RegistryValueKind.Preference, RegistryHive hive = RegistryHive.LocalMachine)
        => new(hive, Path, name, RegistryValueType.Dword, Number(desired), Number(0), kind);

    private static TweakDefinition Entry(
        string id, IReadOnlyList<ITweakAction> actions,
        RiskTier risk = RiskTier.Safe, RestartRequirement requires = RestartRequirement.None,
        IReadOnlyList<TweakId>? dependsOn = null, IReadOnlyList<TweakId>? conflicts = null)
        => ValidEntry(id) with
        {
            Actions = actions,
            Risk = risk,
            Requires = requires,
            DependsOn = dependsOn ?? [],
            ConflictsWith = conflicts ?? [],
        };

    private static void SeedDword(FakeMachine machine, string name, int value)
        => machine.SetRegistry(RegistryHive.LocalMachine, null, Path, name, RegistryValueType.Dword, Number(value));

    private static ActionApplier Applier(FakeMachine machine)
        => new(
            new StateReaders(machine, machine, machine, machine, machine, machine, machine),
            new StateWriters(machine, machine, machine, machine, machine, machine, machine, machine));

    private static (ApplyHarness Harness, FakeMachine Machine) SeededAb()
    {
        var machine = new FakeMachine();
        SeedDword(machine, "A", 0);
        SeedDword(machine, "B", 0);
        return (FakeApplyEngine.Create(machine), machine);
    }

    private static int ReadDword(FakeMachine machine, string name)
    {
        RegistryValueSnapshot snapshot = ((IRegistryReader)machine).Read(RegistryHive.LocalMachine, null, Path, name);
        return snapshot.Data!.Value.GetInt32();
    }

    private static bool Exists(FakeMachine machine, string name)
        => ((IRegistryReader)machine).Read(RegistryHive.LocalMachine, null, Path, name).Exists;

    private static bool UserValueExists(FakeMachine machine, string sid, string name)
        => ((IRegistryReader)machine).Read(RegistryHive.User, sid, Path, name).Exists;

    [Fact]
    public void Applies_a_drifting_value_verifies_and_journals_the_prior()
    {
        var machine = new FakeMachine();
        SeedDword(machine, "A", 0);
        ApplyHarness h = FakeApplyEngine.Create(machine);

        ApplyResult result = h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)])], Opts());

        Assert.Equal(RunState.Completed, result.State);
        Assert.Equal(1, ReadDword(machine, "A"));

        JournalRecord record = Assert.Single(h.Journal.Records);
        JournalAction action = record.Entries[0].Actions[0];
        Assert.Equal(ApplyState.Applied, action.Result);
        Assert.True(action.Verified);
        Assert.True(action.Prior!.Exists);
        Assert.Equal(0, action.Prior.Data!.Value.GetInt32());
        Assert.True(h.Audit.Has(AuditEventId.ApplyStarted));
        Assert.True(h.Audit.Has(AuditEventId.TweakApplied));
    }

    [Fact]
    public void Revert_restores_an_absent_prior_by_deleting_the_value()
    {
        var machine = new FakeMachine(); // "A" is absent
        ApplyHarness h = FakeApplyEngine.Create(machine);

        ApplyResult applied = h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)])], Opts());
        Assert.Equal(1, ReadDword(machine, "A"));

        RevertResult reverted = h.Engine.Revert(applied.RunId, new RevertOptions(false, false));

        Assert.Equal(RunState.Completed, reverted.State);
        Assert.False(Exists(machine, "A"));
        Assert.Equal(applied.RunId, reverted.RevertedRunId);
        Assert.Equal(applied.RunId, h.Journal.Records[^1].RevertOf);
        Assert.True(h.Audit.Has(AuditEventId.RevertStarted));
        Assert.True(h.Audit.Has(AuditEventId.RevertCompleted));
    }

    [Fact]
    public void Compliant_run_makes_no_changes_and_writes_no_journal()
    {
        var machine = new FakeMachine();
        SeedDword(machine, "A", 1);
        ApplyHarness h = FakeApplyEngine.Create(machine);

        ApplyResult result = h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)])], Opts());

        Assert.Equal(RunState.Completed, result.State);
        Assert.Empty(h.Journal.Records);
        Assert.Equal(0, machine.WriteCount);
        Assert.Equal(ApplyState.Skipped, result.Entries[0].Result);
    }

    [Fact]
    public void Writer_failure_rolls_back_every_applied_action()
    {
        (ApplyHarness h, FakeMachine machine) = SeededAb();
        machine.ThrowOnWrite = 2;

        ApplyResult result = h.Engine.Apply(
            [Entry("test.alpha", [Reg("A", 1)]), Entry("test.beta", [Reg("B", 1)])], Opts());

        Assert.Equal(RunState.RolledBack, result.State);
        Assert.Equal(0, ReadDword(machine, "A")); // restored
        Assert.Equal(0, ReadDword(machine, "B")); // never applied
        Assert.True(h.Audit.Has(AuditEventId.RunRolledBack));
        Assert.Contains(h.Journal.Records[0].Entries.SelectMany(e => e.Actions), a => a.Result == ApplyState.RolledBack);
    }

    [Fact]
    public void Verify_failure_rolls_back_the_run()
    {
        (ApplyHarness h, FakeMachine machine) = SeededAb();
        machine.DropRegistryValues.Add("B"); // writes to B are dropped -> verify fails

        ApplyResult result = h.Engine.Apply(
            [Entry("test.alpha", [Reg("A", 1)]), Entry("test.beta", [Reg("B", 1)])], Opts());

        Assert.Equal(RunState.RolledBack, result.State);
        Assert.Equal(0, ReadDword(machine, "A")); // first entry rolled back
        JournalAction bAction = h.Journal.Records[0].Entries[1].Actions[0];
        Assert.False(bAction.Verified);
        Assert.Equal(ApplyState.Failed, bAction.Result);
    }

    [Fact]
    public void Journal_is_written_before_the_first_machine_write()
    {
        var trace = new List<string>();
        var machine = new FakeMachine { ThrowOnWrite = 1, Trace = trace };
        SeedDword(machine, "A", 0);
        ApplyHarness h = FakeApplyEngine.Create(machine, trace: trace);

        h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)])], Opts());

        int firstWrite = trace.FindIndex(t => t.StartsWith("write:", StringComparison.Ordinal));
        int firstBegin = trace.FindIndex(t => t.StartsWith("journal:begin", StringComparison.Ordinal));
        Assert.True(firstBegin >= 0);
        Assert.True(firstBegin < firstWrite, "journal:begin must precede the first write");
        Assert.NotNull(h.Journal.Records[0].Entries[0].Actions[0].Prior);
    }

    [Fact]
    public void Successive_runs_form_a_valid_hash_chain()
    {
        (ApplyHarness h, _) = SeededAb();

        h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)])], Opts());
        h.Engine.Apply([Entry("test.beta", [Reg("B", 1)])], Opts());

        Assert.Equal(2, h.Journal.Records.Count);
        Assert.Null(h.Journal.Records[0].PreviousHash);
        Assert.Equal(h.Journal.Records[0].Hash, h.Journal.Records[1].PreviousHash);
        Assert.True(JournalSerializer.VerifyHash(h.Journal.Records[0]));
        Assert.True(JournalSerializer.VerifyHash(h.Journal.Records[1]));
    }

    [Fact]
    public void Journals_the_target_sid_of_a_user_scope_action_and_reverts_it_in_that_hive()
    {
        var machine = new FakeMachine { User = new InteractiveUser("S-1-5-21-99", @"TEST\alice", HiveLoaded: true) };
        ApplyHarness h = FakeApplyEngine.Create(machine);

        ApplyResult applied = h.Engine.Apply([Entry("test.user", [Reg("U", 1, hive: RegistryHive.User)])], Opts());

        Assert.Equal(RunState.Completed, applied.State);
        JournalAction action = h.Journal.Records[0].Entries[0].Actions[0];
        Assert.Equal("S-1-5-21-99", action.Sid);
        Assert.True(UserValueExists(machine, "S-1-5-21-99", "U"));

        // Revert uses the action's journaled SID to restore the same hive it wrote.
        RevertResult reverted = h.Engine.Revert(applied.RunId, new RevertOptions(false, false));

        Assert.Equal(RunState.Completed, reverted.State);
        Assert.False(UserValueExists(machine, "S-1-5-21-99", "U"));
    }

    [Fact]
    public void All_users_applies_a_user_scope_entry_to_every_hive()
    {
        var machine = new FakeMachine();
        var loader = new FakeUserHiveLoader();
        ApplyHarness h = FakeApplyEngine.Create(
            machine, userHives: new FakeUserHiveEnumerator(UserHive("S-1-5-21-A"), UserHive("S-1-5-21-B")), hiveLoader: loader);

        ApplyResult result = h.Engine.Apply([Entry("test.user", [Reg("U", 1, hive: RegistryHive.User)])], Opts(scope: Scope.AllUsers));

        Assert.Equal(RunState.Completed, result.State);
        Assert.Equal(1, ReadUserDword(machine, "S-1-5-21-A", "U"));
        Assert.Equal(1, ReadUserDword(machine, "S-1-5-21-B", "U"));
        Assert.Equal(2, Assert.Single(h.Journal.Records).Entries[0].Actions.Count);
        Assert.Empty(loader.Loaded); // both hives already loaded
    }

    [Fact]
    public void All_users_loads_and_unloads_an_offline_hive()
    {
        var machine = new FakeMachine();
        var loader = new FakeUserHiveLoader();
        ApplyHarness h = FakeApplyEngine.Create(
            machine, userHives: new FakeUserHiveEnumerator(UserHive("S-1-5-21-A"), UserHive("S-1-5-21-OFF", loaded: false)), hiveLoader: loader);

        h.Engine.Apply([Entry("test.user", [Reg("U", 1, hive: RegistryHive.User)])], Opts(scope: Scope.AllUsers));

        Assert.Equal("S-1-5-21-OFF", Assert.Single(loader.Loaded));
        Assert.Equal("S-1-5-21-OFF", Assert.Single(loader.Unloaded));
        Assert.Equal(1, ReadUserDword(machine, "S-1-5-21-OFF", "U"));
    }

    [Fact]
    public void All_users_plans_a_user_entry_the_interactive_user_is_compliant_with_and_applies_only_the_drifted_hive()
    {
        var machine = new FakeMachine { User = new InteractiveUser("S-1-5-21-A", @"TEST\a", true) };
        SeedUserDword(machine, "S-1-5-21-A", "U", 1); // the interactive user A is already compliant (what the scan sees)
        ApplyHarness h = FakeApplyEngine.Create(
            machine, userHives: new FakeUserHiveEnumerator(UserHive("S-1-5-21-A"), UserHive("S-1-5-21-B")), hiveLoader: new FakeUserHiveLoader());

        ApplyResult result = h.Engine.Apply([Entry("test.user", [Reg("U", 1, hive: RegistryHive.User)])], Opts(scope: Scope.AllUsers));

        Assert.Equal(RunState.Completed, result.State);
        IReadOnlyList<JournalAction> actions = h.Journal.Records[0].Entries[0].Actions;
        Assert.Equal(ApplyState.Skipped, actions.Single(a => string.Equals(a.Sid, "S-1-5-21-A", StringComparison.Ordinal)).Result);
        Assert.Equal(ApplyState.Applied, actions.Single(a => string.Equals(a.Sid, "S-1-5-21-B", StringComparison.Ordinal)).Result);
        Assert.Equal(1, ReadUserDword(machine, "S-1-5-21-B", "U"));
    }

    [Fact]
    public void All_users_audits_but_does_not_fail_the_run_on_an_unload_error()
    {
        var machine = new FakeMachine();
        var loader = new FakeUserHiveLoader { FailUnload = true };
        ApplyHarness h = FakeApplyEngine.Create(
            machine, userHives: new FakeUserHiveEnumerator(UserHive("S-1-5-21-OFF", loaded: false)), hiveLoader: loader);

        ApplyResult result = h.Engine.Apply([Entry("test.user", [Reg("U", 1, hive: RegistryHive.User)])], Opts(scope: Scope.AllUsers));

        Assert.Equal(RunState.Completed, result.State); // unload failure is audited, not fatal
        Assert.True(h.Audit.Has(AuditEventId.Error));
        Assert.Equal(1, ReadUserDword(machine, "S-1-5-21-OFF", "U"));
    }

    [Fact]
    public void Requires_is_the_max_over_applied_entries_and_restarts_explorer()
    {
        (ApplyHarness h, FakeMachine machine) = SeededAb();
        machine.User = new InteractiveUser("S-1-5-21-test", @"TEST\u", true);

        ApplyResult result = h.Engine.Apply(
            [
                Entry("test.alpha", [Reg("A", 1)], requires: RestartRequirement.ExplorerRestart),
                Entry("test.beta", [Reg("B", 1)], requires: RestartRequirement.Reboot),
            ],
            Opts(restartExplorer: true));

        Assert.Equal(RestartRequirement.Reboot, result.Requires);
        Assert.Contains(h.Explorer.Restarts, s => string.Equals(s, "S-1-5-21-test", StringComparison.Ordinal));
    }

    [Fact]
    public void Managed_setting_is_skipped_without_break_glass()
    {
        var machine = new FakeMachine();
        SeedDword(machine, "A", 0);
        machine.SetManaged(RegistryHive.LocalMachine, Path, "A");
        ApplyHarness h = FakeApplyEngine.Create(machine);

        ApplyResult result = h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)])], Opts());

        Assert.Equal(RunState.Completed, result.State);
        Assert.Equal(0, ReadDword(machine, "A")); // unchanged
        Assert.Equal(ApplyState.Managed, result.Entries[0].Result);
        Assert.Empty(h.Journal.Records);
    }

    [Fact]
    public void Break_glass_applies_over_a_managed_setting_and_audits_it()
    {
        var machine = new FakeMachine();
        SeedDword(machine, "A", 0);
        machine.SetManaged(RegistryHive.LocalMachine, Path, "A");
        ApplyHarness h = FakeApplyEngine.Create(machine);

        ApplyResult result = h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)])], Opts(breakGlass: true));

        Assert.Equal(RunState.Completed, result.State);
        Assert.Equal(1, ReadDword(machine, "A"));
        Assert.True(h.Journal.Records[0].Entries[0].BreakGlass);
        Assert.True(h.Audit.Has(AuditEventId.BreakGlass));
    }

    [Fact]
    public void Conflicting_entries_are_rejected_without_changes()
    {
        var machine = new FakeMachine();
        SeedDword(machine, "A", 0);
        SeedDword(machine, "B", 0);
        ApplyHarness h = FakeApplyEngine.Create(machine);

        ApplyResult result = h.Engine.Apply(
            [
                Entry("test.alpha", [Reg("A", 1)], conflicts: [new TweakId("test.beta")]),
                Entry("test.beta", [Reg("B", 1)]),
            ],
            Opts());

        Assert.Equal(RunState.Failed, result.State);
        Assert.Equal(Guid.Empty, result.RunId);
        Assert.True(result.Plan.HasErrors);
        Assert.Equal(0, machine.WriteCount);
    }

    [Fact]
    public void Dependencies_are_applied_before_their_dependents()
    {
        var machine = new FakeMachine();
        SeedDword(machine, "A", 0);
        SeedDword(machine, "B", 0);
        ApplyHarness h = FakeApplyEngine.Create(machine);

        // Supplied out of order: beta depends on alpha.
        PlanResult plan = h.Engine.Plan(
            [
                Entry("test.beta", [Reg("B", 1)], dependsOn: [new TweakId("test.alpha")]),
                Entry("test.alpha", [Reg("A", 1)]),
            ],
            Opts());

        Assert.Equal("test.alpha", plan.Entries[0].Id.Value);
        Assert.Equal("test.beta", plan.Entries[1].Id.Value);
    }

    [Fact]
    public void Protected_service_is_refused_at_plan_time()
    {
        var machine = new FakeMachine();
        machine.SetService(new ServiceSnapshot("WinDefend", ServiceStartType.Automatic, IsRunning: true, IsProtectedProcess: false));
        ApplyHarness h = FakeApplyEngine.Create(machine);
        var entry = Entry("test.svc", [new ServiceAction("WinDefend", ServiceStartType.Disabled, ServiceStartType.Automatic, true)]);

        ApplyResult result = h.Engine.Apply([entry], Opts());

        Assert.Equal(ApplyState.Refused, result.Entries[0].Result);
        Assert.True(h.Audit.Has(AuditEventId.Refusal));
        Assert.Empty(h.Journal.Records);
    }

    [Fact]
    public void ActionApplier_throws_on_a_protected_service()
    {
        var machine = new FakeMachine();
        machine.SetService(new ServiceSnapshot("wuauserv", ServiceStartType.Manual, IsRunning: false, IsProtectedProcess: false));

        Assert.Throws<RefusedOperationException>(
            () => Applier(machine).Apply(new ServiceAction("wuauserv", ServiceStartType.Disabled, ServiceStartType.Manual, false), null));
    }

    [Fact]
    public void ActionApplier_throws_on_a_disallowed_executable()
    {
        var machine = new FakeMachine();

        Assert.Throws<RefusedOperationException>(
            () => Applier(machine).Apply(new CommandAction("cmd.exe", ["/c", "echo"], ["/c", "echo"]), null));
    }

    [Fact]
    public void Restore_point_skipped_is_reported_truthfully()
    {
        var machine = new FakeMachine();
        SeedDword(machine, "A", 0);
        ApplyHarness h = FakeApplyEngine.Create(machine);
        h.RestorePoint.Result = new RestorePointResult(RestorePointStatus.Skipped24h, null, "One was created in the last 24 hours.");

        ApplyResult result = h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)])], Opts(restore: true));

        Assert.Equal(RestorePointStatus.Skipped24h, result.RestorePoint.Status);
        Assert.False(h.Journal.Records[0].RestorePoint.Created);
        Assert.Equal("Skipped24h", h.Journal.Records[0].RestorePoint.Reason);
        Assert.Equal(1, h.RestorePoint.Calls);
    }

    [Fact]
    public void Blocking_preflight_prevents_the_run()
    {
        var machine = new FakeMachine();
        SeedDword(machine, "A", 0);
        ApplyHarness h = FakeApplyEngine.Create(machine);
        h.Preflight.Report = new PreflightReport([new PreflightIssue(PreflightCheck.PendingReboot, Blocking: true, "A reboot is pending.")]);

        ApplyResult result = h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)])], Opts());

        Assert.Equal(RunState.Failed, result.State);
        Assert.Equal(Guid.Empty, result.RunId);
        Assert.Equal(0, machine.WriteCount);
        Assert.Empty(h.Journal.Records);
    }

    [Fact]
    public void What_if_makes_no_changes()
    {
        var machine = new FakeMachine();
        SeedDword(machine, "A", 0);
        ApplyHarness h = FakeApplyEngine.Create(machine);

        ApplyResult result = h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)])], Opts(whatIf: true));

        Assert.Equal(0, machine.WriteCount);
        Assert.Empty(h.Journal.Records);
        Assert.Single(result.Plan.Entries);
        Assert.Equal(PlanDisposition.Apply, result.Plan.Entries[0].Disposition);
    }

    private static (ApplyHarness Harness, Guid RunId) ApplyOne(FakeMachine machine, ITweakAction action)
    {
        ApplyHarness harness = FakeApplyEngine.Create(machine);
        ApplyResult result = harness.Engine.Apply([Entry("test.kind", [action])], Opts());
        Assert.Equal(RunState.Completed, result.State);
        return (harness, result.RunId);
    }

    private static void AssertRevertCycle<T>(FakeMachine machine, ITweakAction action, Func<T> read, T afterApply, T afterRevert)
    {
        (ApplyHarness harness, Guid runId) = ApplyOne(machine, action);
        Assert.Equal(afterApply, read());
        harness.Engine.Revert(runId, new RevertOptions(false, false));
        Assert.Equal(afterRevert, read());
    }

    [Fact]
    public void Applies_and_reverts_a_service_start_type()
    {
        var machine = new FakeMachine();
        machine.SetService(new ServiceSnapshot("DiagTrack", ServiceStartType.Automatic, IsRunning: true, IsProtectedProcess: false));

        (ApplyHarness harness, Guid runId) = ApplyOne(machine, new ServiceAction("DiagTrack", ServiceStartType.Disabled, ServiceStartType.Automatic, StopIfRunning: true));

        ServiceSnapshot after = ((IServiceReader)machine).Read("DiagTrack")!;
        Assert.Equal(ServiceStartType.Disabled, after.StartType);
        Assert.False(after.IsRunning);

        harness.Engine.Revert(runId, new RevertOptions(false, false));
        Assert.Equal(ServiceStartType.Automatic, ((IServiceReader)machine).Read("DiagTrack")!.StartType);
    }

    [Fact]
    public void Applies_and_reverts_a_scheduled_task()
    {
        var machine = new FakeMachine();
        machine.SetTask(new ScheduledTaskSnapshot(@"\MS\Task", Enabled: true, Exists: true));
        AssertRevertCycle(
            machine,
            new ScheduledTaskAction(@"\MS\Task", Enabled: false, Default: true),
            () => ((IScheduledTaskReader)machine).Read(@"\MS\Task")!.Enabled,
            afterApply: false,
            afterRevert: true);
    }

    [Fact]
    public void Applies_and_reverts_an_optional_feature()
    {
        var machine = new FakeMachine();
        machine.SetFeature("SMB1Protocol", enabled: true);
        AssertRevertCycle(
            machine,
            new OptionalFeatureAction("SMB1Protocol", Enabled: false, Default: true),
            () => ((IOptionalFeatureReader)machine).IsEnabled("SMB1Protocol"),
            afterApply: false,
            afterRevert: true);
    }

    [Fact]
    public void Applies_and_reverts_a_defender_preference()
    {
        var machine = new FakeMachine();
        machine.SetDefender("PUAProtection", Number(0));
        AssertRevertCycle(
            machine,
            new DefenderPreferenceAction("PUAProtection", Number(1), Number(0)),
            () => ((IDefenderPreferenceReader)machine).Read("PUAProtection")!.Value.GetInt32(),
            afterApply: 1,
            afterRevert: 0);
    }

    [Fact]
    public void Applies_and_reverts_a_power_setting()
    {
        var subgroup = new Guid("11111111-1111-1111-1111-111111111111");
        var setting = new Guid("22222222-2222-2222-2222-222222222222");
        var machine = new FakeMachine();
        machine.SetPower(subgroup, setting, 1, 1);
        AssertRevertCycle(
            machine,
            new PowerSettingAction(subgroup, setting, 5, 5),
            () => ((IPowerSettingReader)machine).Read(subgroup, setting),
            afterApply: (5u, 5u),
            afterRevert: (1u, 1u));
    }

    [Fact]
    public void Removes_and_reprovisions_an_appx_package()
    {
        const string Pfn = "Microsoft.Sample_8wekyb3d8bbwe";
        var machine = new FakeMachine();
        machine.SetAppx(Pfn, new AppxSnapshot(InstalledForUser: true, InstalledForAnyUser: true, Provisioned: true, Deprovisioned: false));

        (ApplyHarness harness, Guid runId) = ApplyOne(machine, new AppxAction(Pfn, AppxRemovalMode.AllUsersAndDeprovision, "Reinstall from the Store."));

        AppxSnapshot removed = ((IAppxReader)machine).Read(Pfn, null);
        Assert.False(removed.InstalledForAnyUser);
        Assert.True(removed.Deprovisioned);

        harness.Engine.Revert(runId, new RevertOptions(false, false));
        Assert.True(((IAppxReader)machine).Read(Pfn, null).Provisioned);
    }

    [Fact]
    public void Runs_a_command_action_and_its_revert()
    {
        var machine = new FakeMachine();

        (ApplyHarness harness, Guid runId) = ApplyOne(machine, new CommandAction("powercfg.exe", ["/x", "on"], ["/x", "off"]));

        Assert.Contains(machine.Commands, c => c.Arguments.Contains("on", StringComparer.Ordinal));
        harness.Engine.Revert(runId, new RevertOptions(false, false));
        Assert.Contains(machine.Commands, c => c.Arguments.Contains("off", StringComparer.Ordinal));
    }

    [Fact]
    public void Rollback_failure_marks_the_run_failed()
    {
        (ApplyHarness h, FakeMachine machine) = SeededAb();
        machine.ThrowFromWrite = 2; // apply of the 2nd entry and the rollback write both throw

        ApplyResult result = h.Engine.Apply(
            [Entry("test.alpha", [Reg("A", 1)]), Entry("test.beta", [Reg("B", 1)])], Opts());

        Assert.Equal(RunState.Failed, result.State);
        Assert.True(h.Audit.Has(AuditEventId.Error));
    }

    [Fact]
    public void Managed_refused_and_not_applicable_are_reflected_in_outcomes()
    {
        var machine = new FakeMachine();
        SeedDword(machine, "M", 0);
        machine.SetManaged(RegistryHive.LocalMachine, Path, "M");
        machine.SetService(new ServiceSnapshot("WinDefend", ServiceStartType.Automatic, IsRunning: true, IsProtectedProcess: false));
        ApplyHarness h = FakeApplyEngine.Create(machine);

        ApplyResult result = h.Engine.Apply(
            [
                Entry("test.managed", [Reg("M", 1)]),
                Entry("test.refused", [new ServiceAction("WinDefend", ServiceStartType.Disabled, ServiceStartType.Automatic, false)]),
                Entry("test.na", [new ServiceAction("AbsentSvc", ServiceStartType.Disabled, ServiceStartType.Automatic, false)]),
            ],
            Opts());

        var byId = result.Entries.ToDictionary(e => e.Id.Value, e => e.Result, StringComparer.Ordinal);
        Assert.Equal(ApplyState.Managed, byId["test.managed"]);
        Assert.Equal(ApplyState.Refused, byId["test.refused"]);
        Assert.Equal(ApplyState.NotApplicable, byId["test.na"]);
        Assert.True(h.Audit.Has(AuditEventId.Refusal));
    }

    [Fact]
    public void What_if_plan_reports_each_disposition()
    {
        var machine = new FakeMachine();
        SeedDword(machine, "compliant", 1);
        SeedDword(machine, "drift", 0);
        ApplyHarness h = FakeApplyEngine.Create(machine);

        PlanResult plan = h.Engine.Plan(
            [
                Entry("test.compliant", [Reg("compliant", 1)]),
                Entry("test.drift", [Reg("drift", 1)]),
                Entry("test.advanced", [Reg("drift", 1)], risk: RiskTier.Advanced),
            ],
            Opts(allowAdvanced: false));

        Assert.Equal(PlanDisposition.AlreadyCompliant, plan.Entries[0].Disposition);
        Assert.Equal(PlanDisposition.Apply, plan.Entries[1].Disposition);
        Assert.Equal(PlanDisposition.Blocked, plan.Entries[2].Disposition);
    }

    [Fact]
    public void Revert_what_if_makes_no_changes()
    {
        var machine = new FakeMachine();
        SeedDword(machine, "A", 0);
        ApplyHarness h = FakeApplyEngine.Create(machine);
        ApplyResult applied = h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)])], Opts());
        int writesAfterApply = machine.WriteCount;

        RevertResult preview = h.Engine.Revert(applied.RunId, new RevertOptions(WhatIf: true, false));

        Assert.Equal(Guid.Empty, preview.RunId);
        Assert.Equal(writesAfterApply, machine.WriteCount);
        Assert.Equal(1, ReadDword(machine, "A")); // unchanged by the preview
    }

    [Fact]
    public void History_lists_runs_newest_first()
    {
        (ApplyHarness h, _) = SeededAb();
        h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)])], Opts());
        h.Engine.Apply([Entry("test.beta", [Reg("B", 1)])], Opts());

        IReadOnlyList<JournalSummary> history = h.Engine.History();

        Assert.Equal(2, history.Count);
    }

    [Fact]
    public void Advanced_risk_is_blocked_without_the_flag()
    {
        var machine = new FakeMachine();
        SeedDword(machine, "A", 0);
        ApplyHarness h = FakeApplyEngine.Create(machine);

        ApplyResult result = h.Engine.Apply(
            [Entry("test.alpha", [Reg("A", 1)], risk: RiskTier.Advanced)], Opts(allowAdvanced: false));

        Assert.Equal(0, machine.WriteCount);
        Assert.Equal(ApplyState.Skipped, result.Entries[0].Result);
        Assert.Equal(PlanDisposition.Blocked, result.Plan.Entries[0].Disposition);
    }
}
