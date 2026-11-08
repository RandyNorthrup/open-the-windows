using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Engine;

/// <summary>
/// Transactional apply engine (PLAN §5.3). Plans a run (ordering by dependency,
/// flagging conflicts, unmet dependencies, managed and not-applicable entries), then
/// applies it journal-first: every prior state is written to the journal before the
/// first machine change, each action is verified after it is applied, and any
/// failure — including a failed journal update — rolls back every action of the run
/// that was applied or part-applied, in reverse. Runs are serialised machine-wide,
/// and a run a crash interrupted is rolled back before the next one starts. Revert
/// replays a prior run's captured state from the (verified) journal alone.
/// </summary>
public sealed class ApplyEngine
{
    private static readonly RestorePointResult NoRestorePoint =
        new(RestorePointStatus.NotRequested, null, "Restore point not requested.");

    private static readonly PreflightReport PassingPreflight = new([]);

    /// <summary>How long a run waits for another apply, revert or recovery on this machine to finish.</summary>
    private static readonly TimeSpan RunLockTimeout = TimeSpan.FromSeconds(30);

    private const string RunLockBusy = "Another apply or revert is running on this machine; try again when it finishes.";

    private readonly ScanEngine _scan;
    private readonly ActionApplier _applier;
    private readonly ApplyServices _services;
    private readonly IOperatingSystemInfo _os;
    private readonly TimeProvider _time;
    private readonly RunJournal _journal;
    private readonly RunRollback _rollback;

    /// <summary>Creates the engine with its scan engine, action applier and services.</summary>
    public ApplyEngine(
        ScanEngine scan,
        ActionApplier applier,
        ApplyServices services,
        IOperatingSystemInfo os,
        TimeProvider time)
    {
        _scan = scan ?? throw new ArgumentNullException(nameof(scan));
        _applier = applier ?? throw new ArgumentNullException(nameof(applier));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _os = os ?? throw new ArgumentNullException(nameof(os));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _journal = new RunJournal(services, time);
        _rollback = new RunRollback(applier, _journal);
    }

    /// <summary>Plans an apply run without changing anything (the <c>--what-if</c> output).</summary>
    public PlanResult Plan(IEnumerable<TweakDefinition> entries, ApplyOptions options)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(options);
        return ToPlanResult(BuildPlan(entries, options));
    }

    /// <summary>Lists prior runs, newest first.</summary>
    public IReadOnlyList<JournalSummary> History() => _services.Journal.List();

    /// <summary>
    /// Settles any run a crash or power loss left in progress (rolling it back to its
    /// pre-run state), under the machine run lock. Apply and revert do this first on
    /// their own; this entry point lets a front end do it on demand. Returns the ids
    /// of the runs recovered, or <see langword="null"/> when another run holds the lock.
    /// </summary>
    public IReadOnlyList<Guid>? RecoverInterrupted()
    {
        using IDisposable? held = _services.RunLock.TryAcquire(RunLockTimeout);
        return held is null ? null : _rollback.RecoverInterrupted();
    }

    /// <summary>Applies a run transactionally, journaling before the first change and rolling back on any failure.</summary>
    public ApplyResult Apply(IEnumerable<TweakDefinition> entries, ApplyOptions options)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(options);

        PlannedRun run = BuildPlan(entries, options);
        PlanResult plan = ToPlanResult(run);

        if (plan.HasErrors)
        {
            return new ApplyResult(Guid.Empty, RunState.Failed, RestartRequirement.None, NoRestorePoint, PassingPreflight, plan, Outcomes(run, null));
        }

        if (options.WhatIf)
        {
            return new ApplyResult(Guid.Empty, RunState.Completed, plan.Requires, NoRestorePoint, PassingPreflight, plan, Outcomes(run, null));
        }

        EmitRefusalAudits(run);

        if (!run.Items.Any(i => i.Disposition is PlanDisposition.Apply or PlanDisposition.ManagedBreakGlass))
        {
            // Nothing to change (all compliant / managed / not-applicable / blocked / refused): no journal, no restore point.
            return new ApplyResult(Guid.Empty, RunState.Completed, RestartRequirement.None, NoRestorePoint, PassingPreflight, plan, Outcomes(run, null));
        }

        PreflightReport preflight = _services.Preflight.Run(options.Scope);
        if (!preflight.CanProceed)
        {
            _journal.Audit(AuditEventId.Error, "Pre-flight blocked the apply run.", null, null);
            return new ApplyResult(Guid.Empty, RunState.Failed, RestartRequirement.None, NoRestorePoint, preflight, plan, Outcomes(run, null));
        }

        using IDisposable? held = _services.RunLock.TryAcquire(RunLockTimeout);
        if (held is null)
        {
            PlanResult busy = plan with { Errors = [.. plan.Errors, RunLockBusy] };
            return new ApplyResult(Guid.Empty, RunState.Failed, RestartRequirement.None, NoRestorePoint, preflight, busy, Outcomes(run, null));
        }

        _rollback.RecoverInterrupted();
        return ApplyLocked(run, options, plan, preflight);
    }

    /// <summary>The part of an apply that runs while holding the machine run lock.</summary>
    private ApplyResult ApplyLocked(PlannedRun run, ApplyOptions options, PlanResult plan, PreflightReport preflight)
    {
        RestorePointResult restorePoint = options.CreateRestorePoint
            ? _services.RestorePoint.Create(RestoreDescription(options.ProfileName))
            : NoRestorePoint;

        var runId = Guid.NewGuid();

        // For an all-users run, load the hives of users who are not logged on so their
        // prior state can be captured and written; unload them again in the finally.
        // A profile whose hive cannot be loaded is skipped (and audited), not fatal.
        IReadOnlyList<UserProfile> candidates = options.Scope == Scope.AllUsers ? _services.UserHives.Enumerate() : [];
        List<UserProfile> loadedHives = [];
        try
        {
            IReadOnlyList<UserProfile> targetUsers = LoadHives(candidates, loadedHives, runId);
            return RunApply(run, options, restorePoint, runId, targetUsers, plan, preflight);
        }
        finally
        {
            UnloadHives(loadedHives, runId);
        }
    }

    private ApplyResult RunApply(
        PlannedRun run, ApplyOptions options, RestorePointResult restorePoint, Guid runId,
        IReadOnlyList<UserProfile> targetUsers, PlanResult plan, PreflightReport preflight)
    {
        InteractiveUser? user = _services.InteractiveUser.Resolve();
        JournalRecord record = BuildJournalRecord(run, options, restorePoint, runId, user, targetUsers);

        _services.Journal.Begin(record);
        _journal.Audit(AuditEventId.ApplyStarted,
            string.Create(CultureInfo.InvariantCulture, $"Apply '{options.ProfileName}' started ({record.Entries.Count} entr(y/ies))."),
            runId, null);

        RunState state = ExecuteApply(record, runId);
        record.Result = state;
        record.Requires = AggregateRequires(record);
        _journal.TryUpdate(record);

        if (state == RunState.Completed)
        {
            MaybeRestartExplorer(record, run.UserSid, options);
        }

        return new ApplyResult(runId, state, record.Requires, restorePoint, preflight, plan, Outcomes(run, record));
    }

    private List<UserProfile> LoadHives(IReadOnlyList<UserProfile> candidates, List<UserProfile> loaded, Guid runId)
    {
        List<UserProfile> reachable = [];
        foreach (UserProfile profile in candidates)
        {
            if (profile.HiveLoaded)
            {
                reachable.Add(profile);
                continue;
            }

            Exception? failure = Capture(() => _services.HiveLoader.Load(profile.Sid, Path.Combine(profile.ProfilePath, "NTUSER.DAT")));
            if (failure is null)
            {
                loaded.Add(profile);
                reachable.Add(profile);
                _journal.Audit(AuditEventId.ApplyStarted, string.Create(CultureInfo.InvariantCulture, $"Loaded user hive {profile.Sid}."), runId, null);
            }
            else
            {
                _journal.Audit(AuditEventId.Error,
                    string.Create(CultureInfo.InvariantCulture, $"Skipped user {profile.Sid}: their hive could not be loaded ({failure.Message})."), runId, null);
            }
        }

        return reachable;
    }

    private void UnloadHives(List<UserProfile> loaded, Guid runId)
    {
        foreach (UserProfile profile in loaded)
        {
            UnloadHive(profile, runId);
        }
    }

    /// <summary>
    /// Loads the offline hives a revert needs: the profiles whose SID appears on a
    /// journalled action but whose hive is not currently loaded (their owner logged
    /// off since the apply). Machine actions (no SID) and hives already loaded need
    /// nothing. A hive that cannot be loaded leaves its restores to fail and be
    /// reported. The caller unloads whatever this loads.
    /// </summary>
    private void LoadHivesForRevert(JournalRecord original, List<UserProfile> loaded, Guid runId)
    {
        HashSet<string> sids = [.. original.Entries
            .SelectMany(e => e.Actions)
            .Select(a => a.Sid)
            .Where(s => s is not null)
            .Select(s => s!)];
        if (sids.Count == 0)
        {
            return;
        }

        foreach (UserProfile profile in _services.UserHives.Enumerate().Where(p => !p.HiveLoaded && sids.Contains(p.Sid)))
        {
            Exception? failure = Capture(() => _services.HiveLoader.Load(profile.Sid, Path.Combine(profile.ProfilePath, "NTUSER.DAT")));
            if (failure is null)
            {
                loaded.Add(profile);
                _journal.Audit(AuditEventId.RevertStarted, string.Create(CultureInfo.InvariantCulture, $"Loaded user hive {profile.Sid} for revert."), runId, null);
            }
        }
    }

    private void UnloadHive(UserProfile profile, Guid runId)
    {
        // A failed unload leaks a mounted hive but must not mask the run's outcome,
        // so it is audited rather than thrown from the unload finally.
        Exception? failure = Capture(() => _services.HiveLoader.Unload(profile.Sid));
        if (failure is not null)
        {
            _journal.Audit(AuditEventId.Error, string.Create(CultureInfo.InvariantCulture, $"Failed to unload user hive {profile.Sid}: {failure.Message}"), runId, null);
        }
    }

    /// <summary>
    /// Reverts a prior run, restoring each applied action's captured prior state in
    /// reverse order. Refused without changing anything when the journal does not
    /// verify, when a restore would touch a protected location, or — unless
    /// <see cref="RevertOptions.Force"/> — when the run was already reverted or a
    /// later run changed the same settings.
    /// </summary>
    public RevertResult Revert(Guid runId, RevertOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        JournalRecord original = _services.Journal.Load(runId);
        List<JournalEntry> requested = [.. original.Entries
            .Where(e => e.Actions.Any(IsRevertable) && (options.EntryIds is null || options.EntryIds.Contains(e.Id, StringComparer.Ordinal)))
            .Reverse()];
        if (options.EntryIds is not null && requested.Count < options.EntryIds.Count)
        {
            string missing = string.Join(", ", options.EntryIds.Where(id => requested.TrueForAll(e => !string.Equals(e.Id, id, StringComparison.Ordinal))));
            return new RevertResult(Guid.Empty, runId, RunState.Failed,
                [.. options.EntryIds.Select(id => new EntryOutcome(new TweakId(id), ApplyState.NotApplicable, RiskTier.Safe, RestartRequirement.None,
                    string.Create(CultureInfo.InvariantCulture, $"run {runId} changed nothing to revert for: {missing}")))]);
        }

        (string? guarded, bool alreadyReverted, List<JournalEntry> revertable) = RevertGuard.Check(original, requested, _services.Journal, options.Force);
        string? refusal = guarded ?? FirstRestoreRefusal(revertable);
        if (refusal is not null)
        {
            _journal.Audit(AuditEventId.Refusal, string.Create(CultureInfo.InvariantCulture, $"Revert of {runId} refused: {refusal}"), null, null);
            return new RevertResult(Guid.Empty, runId, RunState.Failed,
                [.. revertable.Select(e => new EntryOutcome(new TweakId(e.Id), ApplyState.Refused, e.Risk, e.Requires, refusal))], alreadyReverted);
        }

        if (options.WhatIf)
        {
            IReadOnlyList<EntryOutcome> preview =
                [.. revertable.Select(e => new EntryOutcome(new TweakId(e.Id), ApplyState.Pending, e.Risk, e.Requires, "would revert"))];
            return new RevertResult(Guid.Empty, runId, RunState.Completed, preview);
        }

        using IDisposable? held = _services.RunLock.TryAcquire(RunLockTimeout);
        if (held is null)
        {
            return new RevertResult(Guid.Empty, runId, RunState.Failed,
                [.. revertable.Select(e => new EntryOutcome(new TweakId(e.Id), ApplyState.Skipped, e.Risk, e.Requires, RunLockBusy))]);
        }

        _rollback.RecoverInterrupted();
        return RunRevert(original, revertable, options);
    }

    private RevertResult RunRevert(JournalRecord original, List<JournalEntry> revertable, RevertOptions options)
    {
        var newRunId = Guid.NewGuid();

        // An all-users apply may have written user hives whose owners have since logged off.
        // Load those offline hives first so the revert record captures their real current
        // state (making the revert itself revertible), then restore and unload them.
        List<UserProfile> loadedHives = [];
        JournalRecord record;
        RunState state;
        try
        {
            LoadHivesForRevert(original, loadedHives, newRunId);
            record = BuildRevertRecord(original, revertable, newRunId);
            _services.Journal.Begin(record);
            _journal.Audit(AuditEventId.RevertStarted, string.Create(CultureInfo.InvariantCulture, $"Revert of {original.RunId} started."), newRunId, null);
            state = ExecuteRevert(record, newRunId);
        }
        finally
        {
            UnloadHives(loadedHives, newRunId);
        }

        record.Result = state;
        _journal.TryUpdate(record);

        _journal.Audit(AuditEventId.RevertCompleted, string.Create(CultureInfo.InvariantCulture, $"Revert of {original.RunId} completed ({state})."), newRunId, null);
        MaybeRestartExplorer(record, original.TargetUser?.Sid, new ApplyOptions(false, false, false, false, false, original.Profile, options.RestartExplorer));

        IReadOnlyList<EntryOutcome> outcomes =
            [.. record.Entries.Select(e => new EntryOutcome(new TweakId(e.Id), e.Result, e.Risk, e.Requires, RevertNote(e)))];
        return new RevertResult(newRunId, original.RunId, state, outcomes);
    }

    private static bool IsRevertable(JournalAction action) => action.Result is ApplyState.Applied or ApplyState.Failed;

    private static string? FirstRestoreRefusal(List<JournalEntry> revertable)
        => revertable
            .SelectMany(e => e.Actions.Where(IsRevertable))
            .Select(a => ActionApplier.CheckRestoreRefusal(a.Action, a.Prior))
            .FirstOrDefault(reason => reason is not null);

    private static string? RevertNote(JournalEntry entry) => entry.Result switch
    {
        ApplyState.NotReversible => "the prior state cannot be put back automatically (see the entry's reinstall or manual steps)",
        ApplyState.Failed => "restoring the prior state failed; see the audit log",
        _ => null,
    };

    private PlannedRun BuildPlan(IEnumerable<TweakDefinition> entries, ApplyOptions options)
    {
        List<TweakDefinition> list = [.. entries];
        ScanReport report = _scan.Scan(list, options.ProfileName);
        string? sid = _services.InteractiveUser.Resolve()?.Sid;

        List<PlannedItem> items = [];
        for (int i = 0; i < list.Count; i++)
        {
            TweakDefinition entry = list[i];
            TweakObservation observation = report.Results[i];
            (PlanDisposition disposition, string? note) = Classify(entry, observation, options, sid);
            items.Add(new PlannedItem(entry, observation, disposition, note));
        }

        List<string> errors = [];
        items = OrderByDependency(items, errors);
        items = DetectConflicts(items, errors);
        items = RequireDependencies(items);
        return new PlannedRun(options.ProfileName, sid, items, errors);
    }

    private (PlanDisposition Disposition, string? Note) Classify(
        TweakDefinition entry, TweakObservation observation, ApplyOptions options, string? sid)
    {
        if (Gate(entry, observation, options, sid) is { } gated)
        {
            return gated;
        }

        if (observation.Actions.Any(a => a.Managed != ManagedState.NotManaged))
        {
            return options.BreakGlass
                ? (PlanDisposition.ManagedBreakGlass, "managed setting overridden (break-glass)")
                : (PlanDisposition.Managed, "managed by MDM/Group Policy");
        }

        if (observation.State == ComplianceState.Compliant)
        {
            // Under all-users scope a user-scoped entry that is compliant for the
            // interactive user the scan saw may still be non-compliant in other users'
            // hives, so it must still be planned; the per-user expansion in the journal
            // skips whichever hives are already compliant.
            bool otherHivesMayDiffer = options.Scope == Scope.AllUsers && entry.Actions.Any(ActionApplier.IsUserScoped);
            return otherHivesMayDiffer ? (PlanDisposition.Apply, null) : (PlanDisposition.AlreadyCompliant, null);
        }

        return (PlanDisposition.Apply, null);
    }

    /// <summary>
    /// The checks that rule an entry out before its compliance matters: not applicable
    /// here, per-user with nobody signed in, risk-gated, refused by a safety rule, or
    /// targeting an existing registry value whose type cannot be captured for revert.
    /// </summary>
    private (PlanDisposition Disposition, string? Note)? Gate(
        TweakDefinition entry, TweakObservation observation, ApplyOptions options, string? sid)
    {
        if (observation.State == ComplianceState.NotApplicable)
        {
            return (PlanDisposition.NotApplicable, null);
        }

        if (sid is null && options.Scope != Scope.AllUsers && entry.Actions.Any(ActionApplier.RequiresUser))
        {
            return (PlanDisposition.NotApplicable, "no signed-in user to apply per-user settings to");
        }

        if (entry.Risk >= RiskTier.Breaking && !options.AllowBreaking)
        {
            return (PlanDisposition.Blocked, "requires --allow-breaking (Breaking risk)");
        }

        if (entry.Risk == RiskTier.Advanced && !options.AllowAdvanced)
        {
            return (PlanDisposition.Blocked, "requires --allow-advanced (Advanced risk)");
        }

        foreach (ITweakAction action in entry.Actions)
        {
            if (_applier.CheckRefusal(action) is { } reason)
            {
                return (PlanDisposition.Refused, reason);
            }

            if (_applier.HasUncapturablePrior(action, sid))
            {
                return (PlanDisposition.Refused, "an existing value has a registry type that cannot be captured for revert");
            }
        }

        return null;
    }

    private static List<PlannedItem> OrderByDependency(List<PlannedItem> items, List<string> errors)
    {
        HashSet<TweakId> present = [.. items.Select(i => i.Entry.Id)];
        List<PlannedItem> remaining = [.. items];
        List<PlannedItem> ordered = [];
        HashSet<TweakId> placed = [];

        while (remaining.Count > 0)
        {
            List<PlannedItem> ready = [.. remaining.Where(i =>
                i.Entry.DependsOn.Where(present.Contains).All(placed.Contains))];

            if (ready.Count == 0)
            {
                errors.Add(string.Create(CultureInfo.InvariantCulture,
                    $"Dependency cycle among: {string.Join(", ", remaining.Select(i => i.Entry.Id.Value))}."));
                ordered.AddRange(remaining);
                break;
            }

            foreach (PlannedItem item in ready)
            {
                ordered.Add(item);
                placed.Add(item.Entry.Id);
            }

            remaining.RemoveAll(ready.Contains);
        }

        return ordered;
    }

    private static List<PlannedItem> DetectConflicts(List<PlannedItem> items, List<string> errors)
    {
        HashSet<TweakId> willApply = [.. items
            .Where(i => i.Disposition is PlanDisposition.Apply or PlanDisposition.ManagedBreakGlass)
            .Select(i => i.Entry.Id)];
        HashSet<TweakId> conflicted = [];
        HashSet<string> reported = [];

        foreach (TweakDefinition entry in items.Where(i => willApply.Contains(i.Entry.Id)).Select(i => i.Entry))
        {
            foreach (TweakId other in entry.ConflictsWith.Where(willApply.Contains))
            {
                string key = string.CompareOrdinal(entry.Id.Value, other.Value) < 0
                    ? string.Create(CultureInfo.InvariantCulture, $"{entry.Id.Value}|{other.Value}")
                    : string.Create(CultureInfo.InvariantCulture, $"{other.Value}|{entry.Id.Value}");
                if (reported.Add(key))
                {
                    errors.Add(string.Create(CultureInfo.InvariantCulture,
                        $"Entries {entry.Id.Value} and {other.Value} conflict and cannot be applied together."));
                }

                conflicted.Add(entry.Id);
                conflicted.Add(other);
            }
        }

        return conflicted.Count == 0
            ? items
            : [.. items.Select(i => conflicted.Contains(i.Entry.Id) ? i.As(PlanDisposition.Conflict, "conflicts with another selected entry") : i)];
    }

    /// <summary>
    /// Treats <c>dependsOn</c> as a requirement, not just an ordering: an entry is
    /// blocked unless each dependency is in the run and will be applied, is already
    /// compliant, or is compliant under a managing policy. Repeats until stable so a
    /// chain (shadow stacks → memory integrity → VBS) blocks end to end.
    /// </summary>
    private static List<PlannedItem> RequireDependencies(List<PlannedItem> items)
    {
        bool changed = true;
        while (changed)
        {
            changed = false;
            var byId = items.ToDictionary(i => i.Entry.Id);
            for (int i = 0; i < items.Count; i++)
            {
                PlannedItem item = items[i];
                if (item.Disposition is not (PlanDisposition.Apply or PlanDisposition.ManagedBreakGlass))
                {
                    continue;
                }

                string? unmet = item.Entry.DependsOn.Select(d => UnmetDependency(d, byId)).FirstOrDefault(n => n is not null);
                if (unmet is not null)
                {
                    items[i] = item.As(PlanDisposition.Blocked, unmet);
                    changed = true;
                }
            }
        }

        return items;
    }

    private static string? UnmetDependency(TweakId dependency, Dictionary<TweakId, PlannedItem> byId)
    {
        if (!byId.TryGetValue(dependency, out PlannedItem? required))
        {
            return string.Create(CultureInfo.InvariantCulture, $"requires {dependency.Value}, which is not part of this run");
        }

        bool satisfied = required.Disposition is PlanDisposition.Apply or PlanDisposition.ManagedBreakGlass or PlanDisposition.AlreadyCompliant
            || required.Observation.State is ComplianceState.Compliant or ComplianceState.Managed;
        return satisfied
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"requires {dependency.Value}, which will not be applied ({required.Disposition})");
    }

    private static PlanResult ToPlanResult(PlannedRun run)
    {
        List<PlannedEntry> entries = [.. run.Items.Select(i => new PlannedEntry(
            i.Entry.Id,
            i.Disposition,
            i.Entry.Risk,
            i.Entry.Requires,
            [.. i.Observation.Actions.Select(a => new PlannedAction(a.Action.Kind, ActionTarget.Describe(a.Action, run.UserSid), a.State))],
            i.Note))];
        return new PlanResult(run.ProfileName, entries, run.Errors);
    }

    private JournalRecord BuildJournalRecord(
        PlannedRun run, ApplyOptions options, RestorePointResult restorePoint, Guid runId, InteractiveUser? user,
        IReadOnlyList<UserProfile> targetUsers)
    {
        OperatingSystemFacts facts = _os.GetFacts();
        List<JournalEntry> entries = [];

        // SIDs whose hive we force-load (they were not logged on): a force-loaded NTUSER.DAT
        // does not carry Software\Classes (that lives in UsrClass.dat), so those actions are
        // not applicable there — see ActionState.
        HashSet<string> offlineSids = [.. targetUsers.Where(p => !p.HiveLoaded).Select(p => p.Sid)];

        foreach (PlannedItem item in run.Items.Where(i => i.Disposition is PlanDisposition.Apply or PlanDisposition.ManagedBreakGlass))
        {
            List<JournalAction> actions = [];
            int index = 0;
            for (int i = 0; i < item.Entry.Actions.Count; i++)
            {
                ITweakAction action = item.Entry.Actions[i];
                ComplianceState scanned = i < item.Observation.Actions.Count ? item.Observation.Actions[i].State : ComplianceState.Unknown;

                foreach (string? sid in TargetSids(action, run.UserSid, options.Scope, targetUsers))
                {
                    (ApplyState initial, bool willWrite) = ActionState(action, sid, scanned, options.Scope, offlineSids);
                    actions.Add(new JournalAction
                    {
                        Index = index++,
                        Kind = action.Kind,
                        Target = ActionTarget.Describe(action, sid),
                        Action = action,
                        Sid = sid,
                        Prior = willWrite ? _applier.CapturePrior(action, sid) : null,
                        Desired = ActionApplier.DescribeDesired(action),
                        Result = initial,
                    });
                }
            }

            entries.Add(new JournalEntry
            {
                Id = item.Entry.Id.Value,
                Risk = item.Entry.Risk,
                Requires = item.Entry.Requires,
                Actions = actions,
                Result = ApplyState.Pending,
                BreakGlass = item.Disposition == PlanDisposition.ManagedBreakGlass,
            });
        }

        return new JournalRecord
        {
            RunId = runId,
            StartedAt = _time.GetUtcNow(),
            AppVersion = AppInfo.Version,
            Os = new JournalOs(facts.Build, facts.Revision, facts.EditionId),
            Profile = options.ProfileName,
            Operator = _services.Elevation.ProcessUserName,
            TargetUser = user is null ? null : new JournalUser(user.Sid, user.UserName),
            RestorePoint = ToJournalRestorePoint(restorePoint, options.CreateRestorePoint),
            Entries = entries,
            Result = RunState.InProgress,
            Requires = RestartRequirement.None,
        };
    }

    private JournalRecord BuildRevertRecord(JournalRecord original, List<JournalEntry> revertable, Guid newRunId)
    {
        OperatingSystemFacts facts = _os.GetFacts();
        List<JournalEntry> entries = [];
        foreach (JournalEntry entry in revertable)
        {
            List<JournalAction> actions = [.. entry.Actions
                .Where(IsRevertable)
                .Reverse()
                .Select(a => new JournalAction
                {
                    Index = a.Index,
                    Kind = a.Kind,
                    Target = a.Target,
                    Action = a.Action,
                    Sid = a.Sid,
                    // The prior captured here is the current state (before this revert), so the revert
                    // is itself revertible; the desired state is the original run's prior, restored below.
                    Prior = _applier.CapturePrior(a.Action, a.Sid),
                    Desired = a.Prior ?? new JournalActionState(),
                    Result = ApplyState.Pending,
                })];
            entries.Add(new JournalEntry
            {
                Id = entry.Id,
                Risk = entry.Risk,
                Requires = entry.Requires,
                Actions = actions,
                Result = ApplyState.Pending,
            });
        }

        return new JournalRecord
        {
            RunId = newRunId,
            StartedAt = _time.GetUtcNow(),
            AppVersion = AppInfo.Version,
            Os = new JournalOs(facts.Build, facts.Revision, facts.EditionId),
            Profile = original.Profile,
            Operator = _services.Elevation.ProcessUserName,
            TargetUser = original.TargetUser,
            RestorePoint = new JournalRestorePoint(false, false, RestorePointStatus.NotRequested.ToString(), null),
            Entries = entries,
            Result = RunState.InProgress,
            RevertOf = original.RunId,
        };
    }

    private RunState ExecuteApply(JournalRecord record, Guid runId)
    {
        foreach (JournalEntry entry in record.Entries)
        {
            foreach (JournalAction action in entry.Actions.Where(a => a.Result == ApplyState.Pending))
            {
                Exception? failure = ApplyAndVerify(action);
                action.Result = failure is null ? ApplyState.Applied : ApplyState.Failed;

                // The journal is the recovery record: an action is only kept once the journal says so.
                failure ??= _journal.TryUpdate(record);
                if (failure is not null)
                {
                    _journal.Audit(AuditEventId.TweakFailed, string.Create(CultureInfo.InvariantCulture, $"{entry.Id}: {failure.Message}"), runId, entry.Id);
                    return _rollback.RollBack(record, runId, entry, inFlight: null);
                }
            }

            entry.Result = RunRollback.Aggregate(entry);
            if (entry.BreakGlass)
            {
                _journal.Audit(AuditEventId.BreakGlass, string.Create(CultureInfo.InvariantCulture, $"{entry.Id} applied over a managed setting (break-glass)."), runId, entry.Id);
            }

            _journal.Audit(AuditEventId.TweakApplied, string.Create(CultureInfo.InvariantCulture, $"{entry.Id} applied."), runId, entry.Id);
            if (_journal.TryUpdate(record) is { } journalFailure)
            {
                _journal.Audit(AuditEventId.Error, string.Create(CultureInfo.InvariantCulture, $"Journal update failed: {journalFailure.Message}"), runId, entry.Id);
                return _rollback.RollBack(record, runId, entry, inFlight: null);
            }
        }

        return RunState.Completed;
    }

    private Exception? ApplyAndVerify(JournalAction action)
    {
        Exception? failure = Capture(() => _applier.Apply(action.Action, action.Sid));
        action.AppliedAt = _time.GetUtcNow();
        if (failure is not null)
        {
            return failure;
        }

        bool verified = false;
        failure = Capture(() => verified = _applier.Verify(action.Action, action.Sid));
        action.Verified = verified;
        if (failure is null && !verified)
        {
            return new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"Verification failed for {action.Target}."));
        }

        return failure;
    }

    private RunState ExecuteRevert(JournalRecord record, Guid runId)
    {
        bool clean = true;
        foreach (JournalEntry entry in record.Entries)
        {
            foreach (JournalAction action in entry.Actions.Where(a => a.Result == ApplyState.Pending))
            {
                action.Result = RevertOne(action, runId, entry.Id);
                clean &= action.Result == ApplyState.Applied;
                _journal.TryUpdate(record);
            }

            entry.Result = RunRollback.Aggregate(entry);
        }

        return clean ? RunState.Completed : RunState.Failed;
    }

    private ApplyState RevertOne(JournalAction action, Guid runId, string entryId)
    {
        if (!ActionApplier.CanRestore(action.Action, action.Desired))
        {
            // Do what can be done (an all-users app removal is reprovisioned for future users),
            // but never report the change as undone.
            Capture(() => _applier.Restore(action.Action, action.Desired, action.Sid));
            _journal.Audit(AuditEventId.TweakFailed,
                string.Create(CultureInfo.InvariantCulture, $"Revert of {action.Target}: the prior state cannot be put back automatically."), runId, entryId);
            return ApplyState.NotReversible;
        }

        Exception? failure = Capture(() => _applier.Restore(action.Action, action.Desired, action.Sid));
        action.AppliedAt = _time.GetUtcNow();
        if (failure is null)
        {
            bool verified = false;
            failure = Capture(() => verified = _applier.VerifyRestored(action.Action, action.Desired, action.Sid));
            action.Verified = verified;
            if (failure is null && !verified)
            {
                failure = new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"Revert verification failed for {action.Target}."));
            }
        }

        if (failure is null)
        {
            return ApplyState.Applied;
        }

        _journal.Audit(AuditEventId.TweakFailed, string.Create(CultureInfo.InvariantCulture, $"Revert of {action.Target} failed: {failure.Message}"), runId, entryId);
        return ApplyState.Failed;
    }

    private void MaybeRestartExplorer(JournalRecord record, string? sid, ApplyOptions options)
    {
        if (!options.RestartExplorer || string.IsNullOrEmpty(sid))
        {
            return;
        }

        bool needed = record.Entries.Any(e => e.Result == ApplyState.Applied && e.Requires == RestartRequirement.ExplorerRestart);
        if (needed)
        {
            Capture(() => _services.Explorer.RestartForSession(sid));
        }
    }

    private static RestartRequirement AggregateRequires(JournalRecord record)
        => record.Entries
            .Where(e => e.Result is ApplyState.Applied or ApplyState.NotReversible)
            .Select(e => e.Requires)
            .DefaultIfEmpty(RestartRequirement.None)
            .Max();

    private static IReadOnlyList<EntryOutcome> Outcomes(PlannedRun run, JournalRecord? record)
    {
        Dictionary<string, JournalEntry> journaled = record is null
            ? []
            : record.Entries.ToDictionary(e => e.Id, StringComparer.Ordinal);

        return [.. run.Items.Select(item =>
        {
            ApplyState result = journaled.TryGetValue(item.Entry.Id.Value, out JournalEntry? entry)
                ? entry.Result
                : DispositionToState(item.Disposition);
            return new EntryOutcome(item.Entry.Id, result, item.Entry.Risk, item.Entry.Requires, item.Note);
        })];
    }

    private void EmitRefusalAudits(PlannedRun run)
    {
        foreach (PlannedItem refused in run.Items.Where(i => i.Disposition == PlanDisposition.Refused))
        {
            _journal.Audit(AuditEventId.Refusal, string.Create(CultureInfo.InvariantCulture, $"Refused {refused.Entry.Id}: {refused.Note}"), null, refused.Entry.Id.Value);
        }
    }

    private static (ApplyState Initial, bool WillWrite) InitialActionState(ComplianceState current) => current switch
    {
        ComplianceState.NotApplicable => (ApplyState.NotApplicable, false),
        ComplianceState.Compliant => (ApplyState.Skipped, false),
        _ => (ApplyState.Pending, true),
    };

    /// <summary>
    /// The SIDs an action is journaled and applied under: every target user hive for a
    /// user-scoped action under all-users scope, otherwise the single effective SID
    /// (the interactive user for a user-scoped action, or <see langword="null"/> for a
    /// machine-scope action).
    /// </summary>
    private static IEnumerable<string?> TargetSids(ITweakAction action, string? interactiveSid, Scope scope, IReadOnlyList<UserProfile> targetUsers)
    {
        if (scope == Scope.AllUsers && ActionApplier.IsUserScoped(action))
        {
            return targetUsers.Select(profile => (string?)profile.Sid);
        }

        return [ActionApplier.EffectiveSid(action, interactiveSid)];
    }

    /// <summary>
    /// The initial state and whether the action writes for one target SID. A user-scoped
    /// action under all-users scope is decided by reading that specific hive (the scan
    /// only saw the interactive user); everything else uses the scan's compliance.
    /// </summary>
    private (ApplyState Initial, bool WillWrite) ActionState(
        ITweakAction action, string? sid, ComplianceState scanned, Scope scope, HashSet<string> offlineSids)
    {
        if (scope == Scope.AllUsers && ActionApplier.IsUserScoped(action))
        {
            // A user's Software Classes subtree lives in UsrClass.dat, which a force-loaded
            // NTUSER.DAT does not carry, so writing it into that hive would not take effect at
            // the user's next logon. Mark it not-applicable for an offline user rather than
            // falsely report success. Logged-on users apply it normally.
            if (sid is not null && offlineSids.Contains(sid) && IsUserClassesWrite(action))
            {
                return (ApplyState.NotApplicable, false);
            }

            if (_applier.HasUncapturablePrior(action, sid))
            {
                return (ApplyState.Refused, false);
            }

            return _applier.Verify(action, sid) ? (ApplyState.Skipped, false) : (ApplyState.Pending, true);
        }

        return InitialActionState(scanned);
    }

    /// <summary>
    /// Whether the action writes a value under the user's <c>Software\Classes</c> subtree,
    /// which is backed by UsrClass.dat rather than NTUSER.DAT.
    /// </summary>
    private static bool IsUserClassesWrite(ITweakAction action)
        => action is RegistryAction { Hive: RegistryHive.User } registry
            && registry.Path.StartsWith(@"Software\Classes", StringComparison.OrdinalIgnoreCase);

    private static ApplyState DispositionToState(PlanDisposition disposition) => disposition switch
    {
        PlanDisposition.AlreadyCompliant => ApplyState.Skipped,
        PlanDisposition.Managed => ApplyState.Managed,
        PlanDisposition.NotApplicable => ApplyState.NotApplicable,
        PlanDisposition.Blocked => ApplyState.Skipped,
        PlanDisposition.Refused => ApplyState.Refused,
        PlanDisposition.Conflict => ApplyState.Refused,
        _ => ApplyState.Pending,
    };

    private static JournalRestorePoint ToJournalRestorePoint(RestorePointResult result, bool requested)
        => new(requested, result.Status == RestorePointStatus.Created, result.Status.ToString(), result.SequenceNumber);

    private static string RestoreDescription(string profileName)
        => string.Create(CultureInfo.InvariantCulture, $"{AppInfo.Title}: apply {profileName}");

    /// <summary>Runs <paramref name="operation"/> and returns the exception it threw, or <see langword="null"/>.</summary>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Transactional apply/revert boundary: any writer failure (Win32/COM/WMI/IO) must be caught so the run " +
            "can journal it and roll back, rather than propagate a partially-applied run.")]
    internal static Exception? Capture(Action operation)
    {
        try
        {
            operation();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
