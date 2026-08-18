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
/// flagging conflicts, managed and not-applicable entries), then applies it
/// journal-first: every prior state is written to the journal before the first
/// machine change, each action is verified after it is applied, and any failure
/// rolls back every already-applied action of the run in reverse. Revert replays
/// a prior run's captured state from the journal alone.
/// </summary>
public sealed class ApplyEngine
{
    private static readonly RestorePointResult NoRestorePoint =
        new(RestorePointStatus.NotRequested, null, "Restore point not requested.");

    private static readonly PreflightReport PassingPreflight = new([]);

    private readonly ScanEngine _scan;
    private readonly ActionApplier _applier;
    private readonly ApplyServices _services;
    private readonly IOperatingSystemInfo _os;
    private readonly TimeProvider _time;

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

        PreflightReport preflight = _services.Preflight.Run();
        if (!preflight.CanProceed)
        {
            Audit(AuditEventId.Error, "Pre-flight blocked the apply run.", null, null);
            return new ApplyResult(Guid.Empty, RunState.Failed, RestartRequirement.None, NoRestorePoint, preflight, plan, Outcomes(run, null));
        }

        RestorePointResult restorePoint = options.CreateRestorePoint
            ? _services.RestorePoint.Create(RestoreDescription(options.ProfileName))
            : NoRestorePoint;

        InteractiveUser? user = _services.InteractiveUser.Resolve();
        var runId = Guid.NewGuid();
        JournalRecord record = BuildJournalRecord(run, options, restorePoint, runId, user);

        _services.Journal.Begin(record);
        Audit(AuditEventId.ApplyStarted,
            string.Create(CultureInfo.InvariantCulture, $"Apply '{options.ProfileName}' started ({record.Entries.Count} entr(y/ies))."),
            runId, null);

        RunState state = ExecuteApply(record, runId);
        record.Result = state;
        record.Requires = AggregateRequires(record);
        _services.Journal.Update(record);

        if (state == RunState.Completed)
        {
            MaybeRestartExplorer(record, run.UserSid, options);
        }

        return new ApplyResult(runId, state, record.Requires, restorePoint, preflight, plan, Outcomes(run, record));
    }

    /// <summary>Reverts a prior run, restoring each applied action's captured prior state in reverse order.</summary>
    public RevertResult Revert(Guid runId, RevertOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        JournalRecord original = _services.Journal.Load(runId);
        string? sid = original.TargetUser?.Sid;

        List<JournalEntry> revertable = [.. original.Entries
            .Where(e => e.Actions.Any(a => a.Result == ApplyState.Applied))
            .Reverse()];

        if (options.WhatIf)
        {
            IReadOnlyList<EntryOutcome> preview =
                [.. revertable.Select(e => new EntryOutcome(new TweakId(e.Id), ApplyState.Pending, e.Risk, e.Requires, "would revert"))];
            return new RevertResult(Guid.Empty, runId, RunState.Completed, preview);
        }

        var newRunId = Guid.NewGuid();
        JournalRecord record = BuildRevertRecord(original, revertable, newRunId);

        _services.Journal.Begin(record);
        Audit(AuditEventId.RevertStarted, string.Create(CultureInfo.InvariantCulture, $"Revert of {runId} started."), newRunId, null);

        RunState state = ExecuteRevert(record, newRunId);
        record.Result = state;
        _services.Journal.Update(record);

        Audit(AuditEventId.RevertCompleted, string.Create(CultureInfo.InvariantCulture, $"Revert of {runId} completed ({state})."), newRunId, null);
        MaybeRestartExplorer(record, sid, new ApplyOptions(false, false, false, false, false, original.Profile, options.RestartExplorer));

        IReadOnlyList<EntryOutcome> outcomes =
            [.. record.Entries.Select(e => new EntryOutcome(new TweakId(e.Id), e.Result, e.Risk, e.Requires, null))];
        return new RevertResult(newRunId, runId, state, outcomes);
    }

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
            (PlanDisposition disposition, string? note) = Classify(entry, observation, options);
            items.Add(new PlannedItem(entry, observation, disposition, note));
        }

        List<string> errors = [];
        items = OrderByDependency(items, errors);
        items = DetectConflicts(items, errors);
        return new PlannedRun(options.ProfileName, sid, items, errors);
    }

    private (PlanDisposition Disposition, string? Note) Classify(
        TweakDefinition entry, TweakObservation observation, ApplyOptions options)
    {
        if (observation.State == ComplianceState.NotApplicable)
        {
            return (PlanDisposition.NotApplicable, null);
        }

        if (entry.Risk >= RiskTier.Breaking && !options.AllowBreaking)
        {
            return (PlanDisposition.Blocked, "requires --allow-breaking (Breaking risk)");
        }

        if (entry.Risk >= RiskTier.Advanced && !options.AllowAdvanced)
        {
            return (PlanDisposition.Blocked, "requires --allow-advanced (Advanced risk)");
        }

        foreach (ITweakAction action in entry.Actions)
        {
            if (_applier.CheckRefusal(action) is { } reason)
            {
                return (PlanDisposition.Refused, reason);
            }
        }

        if (observation.Actions.Any(a => a.Managed != ManagedState.NotManaged))
        {
            return options.BreakGlass
                ? (PlanDisposition.ManagedBreakGlass, "managed setting overridden (break-glass)")
                : (PlanDisposition.Managed, "managed by MDM/Group Policy");
        }

        return observation.State == ComplianceState.Compliant
            ? (PlanDisposition.AlreadyCompliant, null)
            : (PlanDisposition.Apply, null);
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

    private JournalRecord BuildJournalRecord(PlannedRun run, ApplyOptions options, RestorePointResult restorePoint, Guid runId, InteractiveUser? user)
    {
        OperatingSystemFacts facts = _os.GetFacts();
        List<JournalEntry> entries = [];

        foreach (PlannedItem item in run.Items.Where(i => i.Disposition is PlanDisposition.Apply or PlanDisposition.ManagedBreakGlass))
        {
            List<JournalAction> actions = [];
            for (int i = 0; i < item.Entry.Actions.Count; i++)
            {
                ITweakAction action = item.Entry.Actions[i];
                ComplianceState current = i < item.Observation.Actions.Count ? item.Observation.Actions[i].State : ComplianceState.Unknown;
                (ApplyState initial, bool willWrite) = InitialActionState(current);
                actions.Add(new JournalAction
                {
                    Index = i,
                    Kind = action.Kind,
                    Target = ActionTarget.Describe(action, run.UserSid),
                    Action = action,
                    Sid = ActionApplier.EffectiveSid(action, run.UserSid),
                    Prior = willWrite ? _applier.CapturePrior(action, run.UserSid) : null,
                    Desired = ActionApplier.DescribeDesired(action),
                    Result = initial,
                });
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
        List<JournalEntry> entries = [];
        foreach (JournalEntry entry in revertable)
        {
            List<JournalAction> actions = [.. entry.Actions
                .Where(a => a.Result == ApplyState.Applied)
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
            Os = original.Os,
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
                if (failure is not null)
                {
                    action.Result = ApplyState.Failed;
                    entry.Result = ApplyState.Failed;
                    _services.Journal.Update(record);
                    Audit(AuditEventId.TweakFailed, string.Create(CultureInfo.InvariantCulture, $"{entry.Id}: {failure.Message}"), runId, entry.Id);
                    return RollBack(record, runId);
                }

                action.Result = ApplyState.Applied;
                _services.Journal.Update(record);
            }

            entry.Result = EntryAggregate(entry);
            if (entry.BreakGlass)
            {
                Audit(AuditEventId.BreakGlass, string.Create(CultureInfo.InvariantCulture, $"{entry.Id} applied over a managed setting (break-glass)."), runId, entry.Id);
            }

            Audit(AuditEventId.TweakApplied, string.Create(CultureInfo.InvariantCulture, $"{entry.Id} applied."), runId, entry.Id);
            _services.Journal.Update(record);
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

    private RunState RollBack(JournalRecord record, Guid runId)
    {
        bool clean = true;
        foreach (JournalEntry entry in Enumerable.Reverse(record.Entries))
        {
            foreach (JournalAction action in Enumerable.Reverse(entry.Actions).Where(a => a.Result == ApplyState.Applied))
            {
                Exception? failure = Capture(() => _applier.Restore(action.Action, action.Prior, action.Sid));
                if (failure is null)
                {
                    action.Result = ApplyState.RolledBack;
                    Audit(AuditEventId.Rollback, string.Create(CultureInfo.InvariantCulture, $"Rolled back {action.Target}."), runId, entry.Id);
                }
                else
                {
                    clean = false;
                    Audit(AuditEventId.Error, string.Create(CultureInfo.InvariantCulture, $"Rollback failed for {action.Target}: {failure.Message}"), runId, entry.Id);
                }

                _services.Journal.Update(record);
            }
        }

        Audit(AuditEventId.RunRolledBack, "Apply run rolled back after a failure.", runId, null);
        return clean ? RunState.RolledBack : RunState.Failed;
    }

    private RunState ExecuteRevert(JournalRecord record, Guid runId)
    {
        bool clean = true;
        foreach (JournalEntry entry in record.Entries)
        {
            foreach (JournalAction action in entry.Actions.Where(a => a.Result == ApplyState.Pending))
            {
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
                    action.Result = ApplyState.Applied;
                }
                else
                {
                    action.Result = ApplyState.Failed;
                    clean = false;
                    Audit(AuditEventId.TweakFailed, string.Create(CultureInfo.InvariantCulture, $"Revert of {action.Target} failed: {failure.Message}"), runId, entry.Id);
                }

                _services.Journal.Update(record);
            }

            entry.Result = EntryAggregate(entry);
        }

        return clean ? RunState.Completed : RunState.Failed;
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
            .Where(e => e.Result == ApplyState.Applied)
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
            Audit(AuditEventId.Refusal, string.Create(CultureInfo.InvariantCulture, $"Refused {refused.Entry.Id}: {refused.Note}"), null, refused.Entry.Id.Value);
        }
    }

    private void Audit(AuditEventId id, string message, Guid? runId, string? entryId)
        => _services.Audit.Write(new AuditEvent(id, _time.GetUtcNow(), message, runId, entryId));

    private static (ApplyState Initial, bool WillWrite) InitialActionState(ComplianceState current) => current switch
    {
        ComplianceState.NotApplicable => (ApplyState.NotApplicable, false),
        ComplianceState.Compliant => (ApplyState.Skipped, false),
        _ => (ApplyState.Pending, true),
    };

    private static ApplyState EntryAggregate(JournalEntry entry)
    {
        if (entry.Actions.Any(a => a.Result == ApplyState.Failed))
        {
            return ApplyState.Failed;
        }

        if (entry.Actions.Any(a => a.Result == ApplyState.Applied))
        {
            return ApplyState.Applied;
        }

        return entry.Actions.Any(a => a.Result == ApplyState.Skipped) ? ApplyState.Skipped : ApplyState.NotApplicable;
    }

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

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Transactional apply/revert boundary: any writer failure (Win32/COM/WMI/IO) must be caught so the run " +
            "can journal it and roll back, rather than propagate a partially-applied run. Recorded in PLAN.md §7.3.")]
    private static Exception? Capture(Action operation)
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
