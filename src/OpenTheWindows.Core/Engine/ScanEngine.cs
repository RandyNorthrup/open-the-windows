using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;

namespace OpenTheWindows.Core.Engine;

/// <summary>
/// Read-only detection engine. Compares each entry's desired state against the
/// machine using the injected readers and produces a <see cref="ScanReport"/>.
/// Never writes to the machine.
/// </summary>
public sealed class ScanEngine
{
    private readonly IRegistryReader _registry;
    private readonly IServiceReader _services;
    private readonly IScheduledTaskReader _tasks;
    private readonly IAppxReader _appx;
    private readonly IOptionalFeatureReader _features;
    private readonly IDefenderPreferenceReader _defender;
    private readonly IPowerSettingReader _power;
    private readonly IInteractiveUserResolver _interactiveUser;
    private readonly IManagedSettingDetector _managed;
    private readonly IOperatingSystemInfo _os;
    private readonly TimeProvider _time;

    /// <summary>Creates the engine with its readers.</summary>
    public ScanEngine(
        IRegistryReader registry,
        IServiceReader services,
        IScheduledTaskReader tasks,
        IAppxReader appx,
        IOptionalFeatureReader features,
        IDefenderPreferenceReader defender,
        IPowerSettingReader power,
        IInteractiveUserResolver interactiveUser,
        IManagedSettingDetector managed,
        IOperatingSystemInfo os,
        TimeProvider time)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
        _appx = appx ?? throw new ArgumentNullException(nameof(appx));
        _features = features ?? throw new ArgumentNullException(nameof(features));
        _defender = defender ?? throw new ArgumentNullException(nameof(defender));
        _power = power ?? throw new ArgumentNullException(nameof(power));
        _interactiveUser = interactiveUser ?? throw new ArgumentNullException(nameof(interactiveUser));
        _managed = managed ?? throw new ArgumentNullException(nameof(managed));
        _os = os ?? throw new ArgumentNullException(nameof(os));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <summary>Scans <paramref name="entries"/> and reports each one's compliance.</summary>
    public ScanReport Scan(IEnumerable<TweakDefinition> entries, string profileName)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        OperatingSystemFacts os = _os.GetFacts();
        string? userSid = _interactiveUser.Resolve()?.Sid;

        List<TweakObservation> results = [];
        foreach (TweakDefinition entry in entries)
        {
            if (!entry.AppliesTo.Matches(os))
            {
                results.Add(new TweakObservation(entry.Id, ComplianceState.NotApplicable, entry.Risk, []));
                continue;
            }

            List<ActionObservation> actions = [.. entry.Actions.Select(action => Evaluate(action, userSid))];
            ComplianceState state = actions.Count == 0 ? ComplianceState.Compliant : actions.Max(o => o.State);
            results.Add(new TweakObservation(entry.Id, state, entry.Risk, actions));
        }

        return new ScanReport(_time.GetUtcNow(), os, profileName, results);
    }

    private ActionObservation Evaluate(ITweakAction action, string? userSid) => action switch
    {
        RegistryAction registry => EvaluateRegistry(registry, userSid),
        ServiceAction service => EvaluateService(service),
        ScheduledTaskAction task => EvaluateTask(task),
        AppxAction appx => EvaluateAppx(appx, userSid),
        OptionalFeatureAction feature => EvaluateFeature(feature),
        DefenderPreferenceAction defender => EvaluateDefender(defender),
        PowerSettingAction power => EvaluatePower(power),
        CommandAction command => new ActionObservation(command, ComplianceState.Unknown,
            "(not detectable)", DescribeCommand(command), ManagedState.NotManaged),
        _ => new ActionObservation(action, ComplianceState.Unknown, "(unsupported)", "(unsupported)", ManagedState.NotManaged),
    };

    private ActionObservation EvaluateRegistry(RegistryAction action, string? userSid)
    {
        ManagedState managed = _managed.Detect(action.Hive, action.Path, action.Name);
        string desired = DescribeRegistry(action.Type, action.Value);
        RegistryValueSnapshot snapshot = _registry.Read(
            action.Hive, action.Hive == RegistryHive.User ? userSid : null, action.Path, action.Name);
        string actual = snapshot is { Exists: true, Data: { } data } ? DescribeRegistry(snapshot.Type, data) : "(absent)";

        bool matchesDesired = snapshot is { Exists: true, Type: { } actualType, Data: { } actualData }
            && actualType == action.Type
            && StateComparison.RegistryEquals(actualData, action.Value, action.Type);

        // A value that does not hold the desired data is drift, whether or not a policy owns it.
        // A managed value that is absent or different in the live hive — for example a policy write
        // whose revert did not complete, leaving the value in Registry.pol but not in effect — must
        // not be reported as compliant merely because it is policy-owned. Only a value that actually
        // matches counts as Managed (owned and satisfied) or Compliant (unmanaged and satisfied); the
        // ManagedState is still reported separately so planning keeps deferring to Group Policy / MDM.
        ComplianceState state;
        if (!matchesDesired)
        {
            state = ComplianceState.Drift;
        }
        else
        {
            state = managed != ManagedState.NotManaged ? ComplianceState.Managed : ComplianceState.Compliant;
        }

        return new ActionObservation(action, state, actual, desired, managed);
    }

    private ActionObservation EvaluateService(ServiceAction action)
    {
        string desired = action.StartType.ToString();
        ServiceSnapshot? snapshot = _services.Read(action.Name);
        if (snapshot is null)
        {
            return new ActionObservation(action, ComplianceState.NotApplicable, "(absent)", desired, ManagedState.NotManaged);
        }

        ComplianceState state = snapshot.StartType == action.StartType ? ComplianceState.Compliant : ComplianceState.Drift;
        return new ActionObservation(action, state, snapshot.StartType.ToString(), desired, ManagedState.NotManaged);
    }

    private ActionObservation EvaluateTask(ScheduledTaskAction action)
    {
        string desired = action.Enabled ? "enabled" : "disabled";
        ScheduledTaskSnapshot? snapshot = _tasks.Read(action.Path);
        if (snapshot is null || !snapshot.Exists)
        {
            return new ActionObservation(action, ComplianceState.NotApplicable, "(absent)", desired, ManagedState.NotManaged);
        }

        ComplianceState state = snapshot.Enabled == action.Enabled ? ComplianceState.Compliant : ComplianceState.Drift;
        return new ActionObservation(action, state, snapshot.Enabled ? "enabled" : "disabled", desired, ManagedState.NotManaged);
    }

    private ActionObservation EvaluateAppx(AppxAction action, string? userSid)
    {
        AppxSnapshot snapshot = _appx.Read(action.PackageFamilyName, userSid);
        bool compliant = StateComparison.AppxCompliant(snapshot, action.Mode);
        string desired = action.Mode == AppxRemovalMode.CurrentUser
            ? "removed for the user"
            : "removed for all users and deprovisioned";
        string actual = string.Create(CultureInfo.InvariantCulture,
            $"user={snapshot.InstalledForUser}, anyUser={snapshot.InstalledForAnyUser}, deprovisioned={snapshot.Deprovisioned}");
        return new ActionObservation(action, compliant ? ComplianceState.Compliant : ComplianceState.Drift, actual, desired, ManagedState.NotManaged);
    }

    private ActionObservation EvaluateFeature(OptionalFeatureAction action)
    {
        string desired = action.Enabled ? "enabled" : "disabled";
        bool? enabled = _features.IsEnabled(action.Name);
        if (enabled is not { } isEnabled)
        {
            return new ActionObservation(action, ComplianceState.NotApplicable, "(absent)", desired, ManagedState.NotManaged);
        }

        ComplianceState state = isEnabled == action.Enabled ? ComplianceState.Compliant : ComplianceState.Drift;
        return new ActionObservation(action, state, isEnabled ? "enabled" : "disabled", desired, ManagedState.NotManaged);
    }

    private ActionObservation EvaluateDefender(DefenderPreferenceAction action)
    {
        string desired = action.Value.GetRawText();
        JsonElement? actual = _defender.Read(action.Property);
        if (actual is not { } actualValue)
        {
            return new ActionObservation(action, ComplianceState.Unknown, "(unreadable)", desired, ManagedState.NotManaged);
        }

        ComplianceState state = StateComparison.JsonEquals(actualValue, action.Value) ? ComplianceState.Compliant : ComplianceState.Drift;
        return new ActionObservation(action, state, actualValue.GetRawText(), desired, ManagedState.NotManaged);
    }

    private ActionObservation EvaluatePower(PowerSettingAction action)
    {
        string desired = string.Create(CultureInfo.InvariantCulture, $"AC={action.AcValue}, DC={action.DcValue}");
        (uint Ac, uint Dc)? read = _power.Read(action.SubgroupGuid, action.SettingGuid);
        if (read is not { } value)
        {
            return new ActionObservation(action, ComplianceState.NotApplicable, "(absent)", desired, ManagedState.NotManaged);
        }

        bool compliant = value.Ac == action.AcValue && value.Dc == action.DcValue;
        string actual = string.Create(CultureInfo.InvariantCulture, $"AC={value.Ac}, DC={value.Dc}");
        return new ActionObservation(action, compliant ? ComplianceState.Compliant : ComplianceState.Drift, actual, desired, ManagedState.NotManaged);
    }

    private static string DescribeCommand(CommandAction action)
        => string.Create(CultureInfo.InvariantCulture, $"{action.Executable} {string.Join(' ', action.Arguments)}");

    private static string DescribeRegistry(RegistryValueType? type, JsonElement value)
        => string.Create(CultureInfo.InvariantCulture, $"{type} {value.GetRawText()}");
}
