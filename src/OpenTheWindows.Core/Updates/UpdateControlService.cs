using System.Buffers;
using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.Core.Updates;

/// <summary>
/// The Windows Update guardrail engine (spec M4): pause, resume and status, all
/// routed through the transactional apply engine so every change is journaled and
/// reversible. Pause writes the six Settings-app <c>UX\Settings</c> values
/// (research 03 §5.1); resume reverts every active Open the Windows pause and
/// triggers a fresh scan (<c>usoclient StartScan</c>); status reads the effective
/// state and warns when a release is within 90 days of end-of-servicing.
/// </summary>
public sealed class UpdateControlService
{
    /// <summary>The journal profile name pause runs are recorded under, so resume can find them.</summary>
    public const string PauseProfileName = "updates-pause";

    /// <summary>The synthetic catalogue id of the runtime pause entry.</summary>
    public const string PauseEntryId = "updates.pause.session";

    /// <summary>The allow-listed executable and argument that triggers a fresh update scan.</summary>
    public const string ScanExecutable = "usoclient.exe";

    private const string ScanArgument = "StartScan";
    private static readonly TimeSpan ScanTimeout = TimeSpan.FromMinutes(2);
    private static readonly JsonElement JsonNull = ParseNull();
    private static readonly IReadOnlyList<Uri> PauseSources =
        [new("https://learn.microsoft.com/windows/deployment/update/waas-configure-wufb")];

    private readonly ApplyEngine _engine;
    private readonly IRegistryReader _registry;
    private readonly ICommandRunner _runner;
    private readonly IAuditSink _audit;
    private readonly IOperatingSystemInfo _os;
    private readonly EndOfServicingCalendar _calendar;
    private readonly PauseCalculator _pause;
    private readonly TimeProvider _time;

    /// <summary>Creates the service over the apply engine and the OS adapters it needs.</summary>
    public UpdateControlService(
        ApplyEngine engine,
        IRegistryReader registry,
        ICommandRunner runner,
        IAuditSink audit,
        IOperatingSystemInfo os,
        EndOfServicingCalendar calendar,
        TimeProvider time,
        int extendedCeilingDays = PauseCalculator.DefaultExtendedCeilingDays)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _os = os ?? throw new ArgumentNullException(nameof(os));
        _calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _pause = new PauseCalculator(time, extendedCeilingDays);
    }

    /// <summary>
    /// Pauses updates for <paramref name="days"/> days. Without
    /// <paramref name="extended"/> the cap is 35 days; with it, up to the configured
    /// ceiling as an Advanced-risk change that also writes an Event 4002. The change
    /// is journaled and reversible.
    /// </summary>
    public PauseOutcome Pause(int days, bool extended = false, bool whatIf = false)
    {
        PausePlan plan = _pause.Calculate(days, extended);

        if (plan.Extended && !whatIf)
        {
            _audit.Write(new AuditEvent(
                AuditEventId.ExtendedPause,
                _time.GetUtcNow(),
                string.Create(CultureInfo.InvariantCulture,
                    $"Extended Windows Update pause applied: {plan.Days} days, until {plan.Expiry.UtcDateTime:yyyy-MM-dd} (beyond Microsoft's 35-day cap)."),
                null,
                PauseEntryId));
        }

        var options = new ApplyOptions(
            WhatIf: whatIf,
            CreateRestorePoint: false,
            BreakGlass: false,
            AllowAdvanced: plan.Extended,
            AllowBreaking: false,
            PauseProfileName,
            RestartExplorer: false);

        ApplyResult result = _engine.Apply([BuildPauseEntry(plan)], options);
        return new PauseOutcome(plan, result, plan.Extended);
    }

    /// <summary>
    /// Resumes updates by reverting <em>every</em> active Open the Windows pause run
    /// (newest first, so a re-pause that stacked on an earlier one is fully unwound
    /// back to the pre-pause state) and triggering a fresh update scan. Reverting
    /// only the latest pause would leave an earlier one in force, so the reported
    /// state would still be "paused"; resume must clear the pause outright.
    /// </summary>
    public ResumeOutcome Resume(bool whatIf = false)
    {
        IReadOnlyList<JournalSummary> pauses = FindActivePauses();
        if (pauses.Count == 0)
        {
            return new ResumeOutcome(false, null, null, !whatIf && TriggerScan());
        }

        RevertResult? firstFailure = null;
        RevertResult? lastRevert = null;
        Guid newestRunId = pauses[0].RunId;
        foreach (JournalSummary pause in pauses)
        {
            RevertResult revert = _engine.Revert(pause.RunId, new RevertOptions(whatIf, RestartExplorer: false));
            lastRevert = revert;
            if (revert.State == RunState.Failed)
            {
                firstFailure ??= revert;
            }
        }

        bool scanned = !whatIf && TriggerScan();
        return new ResumeOutcome(true, newestRunId, firstFailure ?? lastRevert, scanned);
    }

    /// <summary>Reads the effective update state for the status dashboard.</summary>
    public UpdateStatus Status()
    {
        OperatingSystemFacts facts = _os.GetFacts();
        EndOfServicingEditionFamily family = FamilyForEdition(facts.EditionId);
        DateTimeOffset now = _time.GetUtcNow();

        EndOfServicingRelease? release = _calendar.Find(facts.DisplayVersion, family);
        int? days = _calendar.DaysUntilEndOfService(facts.DisplayVersion, family, now);
        bool warning = _calendar.IsWithinWarningWindow(facts.DisplayVersion, family, now);

        (bool paused, DateTimeOffset? until) = ReadPauseState(now);
        string? target = ReadTargetVersion();
        int? targetDays = target is null ? null : _calendar.DaysUntilEndOfService(target, family, now);
        bool targetWarning = target is not null && _calendar.IsWithinWarningWindow(target, family, now);

        return new UpdateStatus(
            facts.DisplayVersion, facts.EditionId, family, release?.EndOfService, days, warning,
            paused, until, target, targetDays, targetWarning);
    }

    private IReadOnlyList<JournalSummary> FindActivePauses()
    {
        IReadOnlyList<JournalSummary> history = _engine.History();
        HashSet<Guid> reverted = [.. history.Where(h => h.RevertOf is not null).Select(h => h.RevertOf!.Value)];
        return
        [
            .. history
                .Where(h => string.Equals(h.Profile, PauseProfileName, StringComparison.Ordinal)
                    && h.Result == RunState.Completed
                    && h.RevertOf is null
                    && !reverted.Contains(h.RunId))
                .OrderByDescending(h => h.StartedAt),
        ];
    }

    private bool TriggerScan()
    {
        CommandResult result = _runner.Run(ScanExecutable, [ScanArgument], ScanTimeout);
        return !result.TimedOut && result.ExitCode == 0;
    }

    private (bool Paused, DateTimeOffset? Until) ReadPauseState(DateTimeOffset now)
    {
        RegistryValueSnapshot snapshot = _registry.Read(
            RegistryHive.LocalMachine, null, WindowsUpdateSettings.UxSettingsPath, WindowsUpdateSettings.PauseUpdatesExpiryTime);
        if (snapshot is not { Exists: true, Data: { } data } || data.ValueKind != JsonValueKind.String)
        {
            return (false, null);
        }

        string text = data.GetString() ?? string.Empty;
        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset until))
        {
            return (false, null);
        }

        return (until > now, until);
    }

    private string? ReadTargetVersion()
    {
        RegistryValueSnapshot snapshot = _registry.Read(
            RegistryHive.LocalMachine, null, WindowsUpdateSettings.PolicyPath, WindowsUpdateSettings.TargetReleaseVersionInfo);
        if (snapshot is not { Exists: true, Data: { } data } || data.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string text = data.GetString() ?? string.Empty;
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static EndOfServicingEditionFamily FamilyForEdition(string editionId)
        => WindowsEditions.FromEditionId(editionId) is { } edition
            ? EndOfServicingCalendar.FamilyFor(edition)
            : EndOfServicingEditionFamily.Consumer;

    private static TweakDefinition BuildPauseEntry(PausePlan plan)
    {
        List<ITweakAction> actions = [.. plan.Values.Select(value => (ITweakAction)new RegistryAction(
            RegistryHive.LocalMachine,
            WindowsUpdateSettings.UxSettingsPath,
            value.Name,
            RegistryValueType.Sz,
            JsonString(value.Value),
            JsonNull,
            RegistryValueKind.Preference))];

        IReadOnlyList<string> sideEffects = plan.Extended
            ? ["The device stays unpatched for longer than Microsoft's supported 35-day limit; security updates are deferred until you resume."]
            : [];

        return new TweakDefinition(
            new TweakId(PauseEntryId),
            "Pause Windows Update",
            string.Create(CultureInfo.InvariantCulture, $"Pauses Windows Update downloads and installs for {plan.Days} days."),
            "The Settings-app pause is the only supported, reversible way to stop updates; it writes six UX\\Settings values that the update client honours.",
            Category.Updates,
            Level.Basic,
            plan.Extended ? RiskTier.Advanced : RiskTier.Safe,
            Scope.Machine,
            TweakStatus.Verified,
            Applicability.Any,
            actions,
            RestartRequirement.None,
            [],
            [],
            null,
            sideEffects,
            PauseSources,
            [],
            ["windows-update", "pause"]);
    }

    private static JsonElement JsonString(string value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStringValue(value);
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private static JsonElement ParseNull()
    {
        using var document = JsonDocument.Parse("null");
        return document.RootElement.Clone();
    }
}
