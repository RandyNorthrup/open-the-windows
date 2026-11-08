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
            new StateReaders(machine, machine, machine, machine, machine, machine, machine, machine),
            new StateWriters(machine, machine, machine, machine, machine, machine, machine, machine, machine));

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
    public void Apply_and_revert_a_security_policy_setting_round_trips()
    {
        var machine = new FakeMachine();
        machine.SetSecurityPolicy(SecurityPolicySection.SystemAccess, "MinimumPasswordLength", "0");
        ApplyHarness h = FakeApplyEngine.Create(machine);
        var entry = Entry("test.password.length",
            [new SecurityPolicyAction(SecurityPolicySection.SystemAccess, "MinimumPasswordLength", "14", "0")]);

        ApplyResult applied = h.Engine.Apply([entry], Opts());
        Assert.Equal(RunState.Completed, applied.State);
        Assert.Equal("14", ((ISecurityPolicyReader)machine).Read(SecurityPolicySection.SystemAccess, "MinimumPasswordLength"));

        RevertResult reverted = h.Engine.Revert(applied.RunId, new RevertOptions(false, false));

        Assert.Equal(RunState.Completed, reverted.State);
        Assert.Equal("0", ((ISecurityPolicyReader)machine).Read(SecurityPolicySection.SystemAccess, "MinimumPasswordLength"));
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
        JournalEntry bEntry = h.Journal.Records[0].Entries[1];
        Assert.False(bEntry.Actions[0].Verified);
        Assert.Equal(ApplyState.Failed, bEntry.Result); // the entry that failed stays reported as failed
        Assert.Equal(ApplyState.RolledBack, bEntry.Actions[0].Result); // and its own write is restored too
    }

    [Fact]
    public void A_failed_action_that_had_already_changed_something_is_restored()
    {
        var machine = new FakeMachine();
        machine.SetService(new ServiceSnapshot("Fax", ServiceStartType.Manual, IsRunning: true, IsProtectedProcess: false));

        // Write 1 (start type) succeeds, write 2 (stopping the service) throws: the action is half-applied.
        machine.ThrowOnWrite = 2;
        ApplyHarness h = FakeApplyEngine.Create(machine);
        ApplyResult result = h.Engine.Apply([Entry("test.fax", [new ServiceAction("Fax", ServiceStartType.Disabled, ServiceStartType.Manual, true)])], Opts());

        Assert.Equal(RunState.RolledBack, result.State);
        Assert.Equal(ServiceStartType.Manual, ((IServiceReader)machine).Read("Fax")!.StartType);
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
    public void All_users_revert_reloads_an_offline_hive_to_restore_it()
    {
        // A logged-off user's hive must be loaded again for the revert, or the value the apply
        // wrote could never be undone — reversibility must survive the user signing out.
        var machine = new FakeMachine();
        var loader = new FakeUserHiveLoader();
        ApplyHarness h = FakeApplyEngine.Create(
            machine, userHives: new FakeUserHiveEnumerator(UserHive("S-1-5-21-OFF", loaded: false)), hiveLoader: loader);

        ApplyResult applied = h.Engine.Apply([Entry("test.user", [Reg("U", 1, hive: RegistryHive.User)])], Opts(scope: Scope.AllUsers));
        Assert.Equal(1, ReadUserDword(machine, "S-1-5-21-OFF", "U"));

        RevertResult reverted = h.Engine.Revert(applied.RunId, new RevertOptions(false, false));

        Assert.Equal(RunState.Completed, reverted.State);
        Assert.Equal(2, loader.Loaded.Count);   // loaded once for the apply, once for the revert
        Assert.Equal(2, loader.Unloaded.Count);
        Assert.All(loader.Loaded, s => Assert.Equal("S-1-5-21-OFF", s));
        Assert.False(((IRegistryReader)machine).Read(RegistryHive.User, "S-1-5-21-OFF", Path, "U").Exists); // restored to absent
    }

    [Fact]
    public void All_users_marks_a_software_classes_user_entry_not_applicable_for_an_offline_hive()
    {
        // Software\Classes lives in UsrClass.dat, which a force-loaded NTUSER.DAT does not carry,
        // so an offline user must be reported not-applicable, never falsely applied; a logged-on
        // user (whose Software\Classes is live) applies it normally.
        var machine = new FakeMachine();
        var classesAction = new RegistryAction(
            RegistryHive.User, @"Software\Classes\CLSID\{TEST}", "V", RegistryValueType.Dword, Number(0), Number(1), RegistryValueKind.Preference);
        ApplyHarness h = FakeApplyEngine.Create(
            machine,
            userHives: new FakeUserHiveEnumerator(UserHive("S-1-5-21-ON"), UserHive("S-1-5-21-OFF", loaded: false)),
            hiveLoader: new FakeUserHiveLoader());

        h.Engine.Apply([Entry("test.classes", [classesAction])], Opts(scope: Scope.AllUsers));

        IReadOnlyList<JournalAction> actions = h.Journal.Records[0].Entries[0].Actions;
        Assert.Equal(ApplyState.NotApplicable, actions.Single(a => string.Equals(a.Sid, "S-1-5-21-OFF", StringComparison.Ordinal)).Result);
        Assert.Equal(ApplyState.Applied, actions.Single(a => string.Equals(a.Sid, "S-1-5-21-ON", StringComparison.Ordinal)).Result);
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

        (ApplyHarness harness, Guid runId) = ApplyOne(machine, new CommandAction("powercfg.exe", ["/hibernate", "off"], ["/hibernate", "on"]));

        Assert.Contains(machine.Commands, c => c.Arguments.Contains("off", StringComparer.Ordinal));
        harness.Engine.Revert(runId, new RevertOptions(false, false));
        Assert.Contains(machine.Commands, c => c.Arguments.Contains("on", StringComparer.Ordinal));
    }

    [Fact]
    public void A_command_outside_its_permitted_argument_shapes_is_refused_and_never_run()
    {
        var machine = new FakeMachine();
        ApplyHarness h = FakeApplyEngine.Create(machine);

        ApplyResult result = h.Engine.Apply([Entry("test.cmd", [new CommandAction("powercfg.exe", ["/import", @"C:\evil.pow"], ["/hibernate", "on"])])], Opts());

        Assert.Equal(ApplyState.Refused, Assert.Single(result.Entries).Result);
        Assert.Empty(machine.Commands);
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

    [Fact]
    public void A_run_lock_held_by_another_process_fails_the_apply_without_writing()
    {
        var machine = new FakeMachine();
        SeedDword(machine, "A", 0);
        ApplyHarness h = FakeApplyEngine.Create(machine, runLock: new FakeRunLock { HeldElsewhere = true });

        ApplyResult result = h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)])], Opts());

        Assert.Equal(RunState.Failed, result.State);
        Assert.Contains(result.Plan.Errors, e => e.Contains("Another apply", StringComparison.Ordinal));
        Assert.Equal(0, machine.WriteCount);
        Assert.Empty(h.Journal.Records);
    }

    [Fact]
    public void A_run_interrupted_mid_write_is_rolled_back_before_the_next_apply()
    {
        (ApplyHarness h, FakeMachine machine) = SeededAb();
        h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)])], Opts());

        // Simulate a crash: the write landed but the journal still says the run and the action are in flight.
        JournalRecord interrupted = h.Journal.Records[0];
        interrupted.Result = RunState.InProgress;
        interrupted.Entries[0].Actions[0].Result = ApplyState.Pending;

        h.Engine.Apply([Entry("test.beta", [Reg("B", 1)])], Opts());

        Assert.Equal(RunState.RolledBack, interrupted.Result);
        Assert.Equal(0, ReadDword(machine, "A"));
        Assert.Equal(1, ReadDword(machine, "B"));
    }

    [Fact]
    public void A_run_interrupted_after_every_action_finished_is_marked_completed_not_undone()
    {
        (ApplyHarness h, FakeMachine machine) = SeededAb();
        h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)])], Opts());
        JournalRecord interrupted = h.Journal.Records[0];
        interrupted.Result = RunState.InProgress;

        IReadOnlyList<Guid>? recovered = h.Engine.RecoverInterrupted();

        Assert.Equal(interrupted.RunId, Assert.Single(recovered!));
        Assert.Equal(RunState.Completed, interrupted.Result);
        Assert.Equal(1, ReadDword(machine, "A"));
    }

    [Fact]
    public void A_journal_update_failure_rolls_the_run_back()
    {
        (ApplyHarness h, FakeMachine machine) = SeededAb();
        h.Journal.FailUpdateNumber = 1;

        ApplyResult result = h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)]), Entry("test.beta", [Reg("B", 1)])], Opts());

        Assert.Equal(RunState.RolledBack, result.State);
        Assert.Equal(0, ReadDword(machine, "A"));
        Assert.Equal(0, ReadDword(machine, "B"));
    }

    [Fact]
    public void An_unavailable_audit_trail_does_not_interrupt_a_run()
    {
        (ApplyHarness h, FakeMachine machine) = SeededAb();
        h.Audit.FailWrites = true;

        ApplyResult result = h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)])], Opts());

        Assert.Equal(RunState.Completed, result.State);
        Assert.Equal(1, ReadDword(machine, "A"));
    }

    [Fact]
    public void Reverting_a_run_twice_is_refused_unless_forced()
    {
        (ApplyHarness h, FakeMachine machine) = SeededAb();
        ApplyResult applied = h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)])], Opts());
        Assert.Equal(RunState.Completed, h.Engine.Revert(applied.RunId, new RevertOptions(false, false)).State);
        SeedDword(machine, "A", 5);

        RevertResult again = h.Engine.Revert(applied.RunId, new RevertOptions(false, false));

        Assert.Equal(RunState.Failed, again.State);
        Assert.Contains("already reverted", Assert.Single(again.Entries).Note, StringComparison.Ordinal);
        Assert.Equal(5, ReadDword(machine, "A"));
        Assert.Equal(RunState.Completed, h.Engine.Revert(applied.RunId, new RevertOptions(false, false, Force: true)).State);
        Assert.Equal(0, ReadDword(machine, "A"));
    }

    [Fact]
    public void Reverting_an_older_run_that_a_later_run_changed_again_is_refused_unless_forced()
    {
        var time = new MutableTimeProvider(FakeApplyEngine.DefaultInstant);
        var machine = new FakeMachine();
        SeedDword(machine, "A", 0);
        ApplyHarness h = FakeApplyEngine.Create(machine, time: time);
        ApplyResult first = h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)])], Opts());
        time.Advance(TimeSpan.FromMinutes(1));
        h.Engine.Apply([Entry("test.alpha-two", [Reg("A", 2)])], Opts());
        time.Advance(TimeSpan.FromMinutes(1));

        RevertResult refused = h.Engine.Revert(first.RunId, new RevertOptions(false, false));

        Assert.Equal(RunState.Failed, refused.State);
        Assert.Equal(2, ReadDword(machine, "A"));
        Assert.Equal(RunState.Completed, h.Engine.Revert(first.RunId, new RevertOptions(false, false, Force: true)).State);
        Assert.Equal(0, ReadDword(machine, "A"));
    }

    [Fact]
    public void Revert_refuses_a_journal_that_would_write_a_code_execution_hook()
    {
        var machine = new FakeMachine();
        ApplyHarness h = FakeApplyEngine.Create(machine);
        var hook = new RegistryAction(
            RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\sethc.exe",
            "Debugger", RegistryValueType.Sz, Text("x"), Text(string.Empty),
            RegistryValueKind.Preference);
        var forged = new JournalRecord
        {
            RunId = Guid.NewGuid(),
            StartedAt = FakeApplyEngine.DefaultInstant,
            AppVersion = "0.0.0",
            Os = new JournalOs(26200, 0, "Professional"),
            Profile = "forged",
            Operator = "attacker",
            RestorePoint = new JournalRestorePoint(false, false, "NotRequested", null),
            Result = RunState.Completed,
            Entries =
            [
                new JournalEntry
                {
                    Id = "forged.entry",
                    Actions =
                    [
                        new JournalAction
                        {
                            Kind = hook.Kind,
                            Target = "ifeo",
                            Action = hook,
                            Prior = new JournalActionState { Exists = true, Type = RegistryValueType.Sz, Data = Text("cmd.exe") },
                            Desired = new JournalActionState(),
                            Result = ApplyState.Applied,
                        },
                    ],
                    Result = ApplyState.Applied,
                },
            ],
        };
        h.Journal.Begin(forged);

        RevertResult result = h.Engine.Revert(forged.RunId, new RevertOptions(false, false, Force: true));

        Assert.Equal(RunState.Failed, result.State);
        Assert.Equal(ApplyState.Refused, Assert.Single(result.Entries).Result);
        Assert.Equal(0, machine.WriteCount);
    }

    [Fact]
    public void Reverting_a_per_user_app_removal_reports_it_not_reversible()
    {
        const string Pfn = "Microsoft.Sample_8wekyb3d8bbwe";
        var machine = new FakeMachine { User = new InteractiveUser("S-1-5-21-U", "PC\\u", true) };
        machine.SetAppx(Pfn, new AppxSnapshot(InstalledForUser: true, InstalledForAnyUser: true, Provisioned: true, Deprovisioned: false));

        (ApplyHarness harness, Guid runId) = ApplyOne(machine, new AppxAction(Pfn, AppxRemovalMode.CurrentUser, "Reinstall from the Store."));
        RevertResult reverted = harness.Engine.Revert(runId, new RevertOptions(false, false));

        Assert.Equal(RunState.Failed, reverted.State);
        Assert.Equal(ApplyState.NotReversible, Assert.Single(reverted.Entries).Result);
    }

    [Fact]
    public void An_entry_whose_dependency_is_not_in_the_run_is_blocked()
    {
        (ApplyHarness h, FakeMachine machine) = SeededAb();

        ApplyResult result = h.Engine.Apply(
            [Entry("test.beta", [Reg("B", 1)], dependsOn: [new TweakId("test.alpha")])], Opts());

        EntryOutcome outcome = Assert.Single(result.Entries);
        Assert.Equal(ApplyState.Skipped, outcome.Result);
        Assert.Contains("requires test.alpha", outcome.Note, StringComparison.Ordinal);
        Assert.Equal(0, ReadDword(machine, "B"));
    }

    [Fact]
    public void An_entry_whose_dependency_is_applied_in_the_same_run_proceeds()
    {
        (ApplyHarness h, FakeMachine machine) = SeededAb();

        ApplyResult result = h.Engine.Apply(
            [Entry("test.beta", [Reg("B", 1)], dependsOn: [new TweakId("test.alpha")]), Entry("test.alpha", [Reg("A", 1)])], Opts());

        Assert.Equal(RunState.Completed, result.State);
        Assert.Equal(1, ReadDword(machine, "A"));
        Assert.Equal(1, ReadDword(machine, "B"));
    }

    [Fact]
    public void A_per_user_entry_with_nobody_signed_in_is_not_applicable_instead_of_failing()
    {
        var machine = new FakeMachine { User = null };
        ApplyHarness h = FakeApplyEngine.Create(machine);

        ApplyResult result = h.Engine.Apply([Entry("test.user", [Reg("U", 1, hive: RegistryHive.User)])], Opts());

        Assert.Equal(RunState.Completed, result.State);
        Assert.Equal(ApplyState.NotApplicable, Assert.Single(result.Entries).Result);
        Assert.Equal(0, machine.WriteCount);
    }

    [Fact]
    public void An_existing_value_of_an_uncapturable_type_is_refused_rather_than_destroyed()
    {
        var machine = new FakeMachine();
        machine.SetUncapturableRegistry(RegistryHive.LocalMachine, null, Path, "A");
        ApplyHarness h = FakeApplyEngine.Create(machine);

        ApplyResult result = h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)])], Opts());

        Assert.Equal(ApplyState.Refused, Assert.Single(result.Entries).Result);
        Assert.Equal(0, machine.WriteCount);
    }

    [Fact]
    public void A_user_whose_hive_cannot_be_loaded_is_skipped_and_the_rest_are_applied()
    {
        var machine = new FakeMachine();
        var loader = new FakeUserHiveLoader();
        loader.FailLoadFor.Add("S-1-5-21-STALE");
        ApplyHarness h = FakeApplyEngine.Create(
            machine, userHives: new FakeUserHiveEnumerator(UserHive("S-1-5-21-A"), UserHive("S-1-5-21-STALE", loaded: false)), hiveLoader: loader);

        ApplyResult result = h.Engine.Apply([Entry("test.user", [Reg("U", 1, hive: RegistryHive.User)])], Opts(scope: Scope.AllUsers));

        Assert.Equal(RunState.Completed, result.State);
        Assert.Equal(1, ReadUserDword(machine, "S-1-5-21-A", "U"));
        Assert.Contains(h.Audit.Events, e => e.Message.Contains("S-1-5-21-STALE", StringComparison.Ordinal));
    }

    [Fact]
    public void Revert_starts_again_a_service_the_apply_stopped()
    {
        var machine = new FakeMachine();
        machine.SetService(new ServiceSnapshot("Fax", ServiceStartType.Manual, IsRunning: true, IsProtectedProcess: false));

        (ApplyHarness harness, Guid runId) = ApplyOne(machine, new ServiceAction("Fax", ServiceStartType.Disabled, ServiceStartType.Manual, StopIfRunning: true));
        Assert.False(((IServiceReader)machine).Read("Fax")!.IsRunning);

        harness.Engine.Revert(runId, new RevertOptions(false, false));

        ServiceSnapshot restored = ((IServiceReader)machine).Read("Fax")!;
        Assert.Equal(ServiceStartType.Manual, restored.StartType);
        Assert.True(restored.IsRunning);
    }

    [Fact]
    public void A_per_entry_revert_undoes_only_that_entry_and_a_later_full_revert_finishes_the_rest()
    {
        (ApplyHarness h, FakeMachine machine) = SeededAb();
        ApplyResult applied = h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)]), Entry("test.beta", [Reg("B", 1)])], Opts());

        RevertResult partial = h.Engine.Revert(applied.RunId, new RevertOptions(false, false, EntryIds: ["test.beta"]));

        Assert.Equal(RunState.Completed, partial.State);
        Assert.Equal(1, ReadDword(machine, "A"));
        Assert.Equal(0, ReadDword(machine, "B"));

        RevertResult rest = h.Engine.Revert(applied.RunId, new RevertOptions(false, false));

        Assert.Equal(RunState.Completed, rest.State);
        Assert.Equal("test.alpha", Assert.Single(rest.Entries).Id.Value);
        Assert.Equal(0, ReadDword(machine, "A"));
        Assert.True(h.Engine.Revert(applied.RunId, new RevertOptions(false, false)).AlreadyReverted);
    }

    [Fact]
    public void A_per_entry_revert_of_an_entry_the_run_did_not_change_changes_nothing()
    {
        (ApplyHarness h, FakeMachine machine) = SeededAb();
        ApplyResult applied = h.Engine.Apply([Entry("test.alpha", [Reg("A", 1)])], Opts());
        int writes = machine.WriteCount;

        RevertResult result = h.Engine.Revert(applied.RunId, new RevertOptions(false, false, EntryIds: ["test.unknown"]));

        Assert.Equal(RunState.Failed, result.State);
        Assert.Equal(writes, machine.WriteCount);
    }

    [Fact]
    public void Allowing_breaking_entries_does_not_also_require_allowing_advanced_ones()
    {
        (ApplyHarness h, FakeMachine machine) = SeededAb();

        ApplyResult result = h.Engine.Apply(
            [Entry("test.breaking", [Reg("A", 1)], risk: RiskTier.Breaking)], Opts(allowAdvanced: false, allowBreaking: true));

        Assert.Equal(RunState.Completed, result.State);
        Assert.Equal(1, ReadDword(machine, "A"));
    }

    [Fact]
    public void Revert_everything_reverts_every_apply_run_newest_first()
    {
        var machine = new FakeMachine();
        var time = new MutableTimeProvider(FakeApplyEngine.DefaultInstant);
        ApplyHarness h = FakeApplyEngine.Create(machine, time: time);
        ApplyResult first = h.Engine.Apply([Entry("privacy.test.first", [Reg("A", 1)])], Opts());
        time.Advance(TimeSpan.FromMinutes(1));
        ApplyResult second = h.Engine.Apply([Entry("privacy.test.second", [Reg("B", 1)])], Opts());
        time.Advance(TimeSpan.FromMinutes(1));

        IReadOnlyList<(JournalSummary Run, RevertResult Result)> results = h.Engine.RevertEverything(new RevertOptions(false, false));

        Assert.Equal([second.RunId, first.RunId], results.Select(r => r.Run.RunId));
        Assert.All(results, r => Assert.Equal(RunState.Completed, r.Result.State));
        Assert.False(Exists(machine, "A"));
        Assert.False(Exists(machine, "B"));
    }

    [Fact]
    public void Revert_everything_skips_runs_already_reverted_and_revert_runs()
    {
        var time = new MutableTimeProvider(FakeApplyEngine.DefaultInstant);
        ApplyHarness h = FakeApplyEngine.Create(new FakeMachine(), time: time);
        ApplyResult applied = h.Engine.Apply([Entry("privacy.test.first", [Reg("A", 1)])], Opts());
        time.Advance(TimeSpan.FromMinutes(1));
        h.Engine.Revert(applied.RunId, new RevertOptions(false, false));
        time.Advance(TimeSpan.FromMinutes(1));

        Assert.Empty(h.Engine.RevertEverything(new RevertOptions(false, false)));
    }

    [Fact]
    public void Revert_everything_reports_a_journal_that_fails_verification_and_reverts_nothing_past_it()
    {
        var machine = new FakeMachine();
        var time = new MutableTimeProvider(FakeApplyEngine.DefaultInstant);
        ApplyHarness h = FakeApplyEngine.Create(machine, time: time);
        ApplyResult earlier = h.Engine.Apply([Entry("privacy.test.earlier", [Reg("A", 1)])], Opts());
        time.Advance(TimeSpan.FromMinutes(1));
        ApplyResult forged = h.Engine.Apply([Entry("privacy.test.forged", [Reg("B", 1)])], Opts());
        time.Advance(TimeSpan.FromMinutes(1));
        h.Journal.FailVerification.Add(forged.RunId);

        IReadOnlyList<(JournalSummary Run, RevertResult Result)> results = h.Engine.RevertEverything(new RevertOptions(false, false));

        // The forged run is refused with the integrity reason, and the earlier run is not reverted
        // either: whether the later run overlaps it cannot be checked without trusting its journal.
        (JournalSummary _, RevertResult refused) = Assert.Single(results, r => r.Run.RunId == forged.RunId);
        Assert.Equal(RunState.Failed, refused.State);
        Assert.Equal(ApplyState.Refused, Assert.Single(refused.Entries).Result);
        Assert.Contains("does not verify", refused.Entries[0].Note, StringComparison.Ordinal);
        Assert.Equal(RunState.Failed, Assert.Single(results, r => r.Run.RunId == earlier.RunId).Result.State);
        Assert.True(Exists(machine, "A"));
        Assert.True(Exists(machine, "B"));
    }

    [Fact]
    public void A_run_journal_loads_by_id_and_revert_everything_rejects_null_options()
    {
        ApplyHarness h = FakeApplyEngine.Create(new FakeMachine());
        ApplyResult applied = h.Engine.Apply([Entry("privacy.test.first", [Reg("A", 1)])], Opts());

        JournalRecord record = h.Engine.LoadRun(applied.RunId);

        Assert.Equal("privacy.test.first", Assert.Single(record.Entries).Id);
        Assert.Equal(ApplyState.Applied, record.Entries[0].Result);
        Assert.Throws<ArgumentNullException>(() => h.Engine.RevertEverything(null!));
    }
}
