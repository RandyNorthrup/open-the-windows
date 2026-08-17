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
                results.Add(new TweakObservation(entry.Id, ComplianceState.NotApplicable, []));
                continue;
            }

            List<ActionObservation> actions = [.. entry.Actions.Select(action => Evaluate(action, userSid))];
            ComplianceState state = actions.Count == 0 ? ComplianceState.Compliant : actions.Max(o => o.State);
            results.Add(new TweakObservation(entry.Id, state, actions));
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

        ComplianceState state;
        if (managed != ManagedState.NotManaged)
        {
            state = ComplianceState.Managed;
        }
        else if (!snapshot.Exists || snapshot.Data is not { } actualData || snapshot.Type != action.Type)
        {
            state = ComplianceState.Drift;
        }
        else
        {
            state = RegistryValueEquals(actualData, action.Value, action.Type) ? ComplianceState.Compliant : ComplianceState.Drift;
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
        bool compliant = action.Mode == AppxRemovalMode.CurrentUser
            ? !snapshot.InstalledForUser
            : !snapshot.InstalledForAnyUser && snapshot.Deprovisioned;
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

        ComplianceState state = JsonEquals(actualValue, action.Value) ? ComplianceState.Compliant : ComplianceState.Drift;
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

    private static bool RegistryValueEquals(JsonElement actual, JsonElement desired, RegistryValueType type) => type switch
    {
        RegistryValueType.Dword or RegistryValueType.Qword =>
            actual.ValueKind == JsonValueKind.Number && desired.ValueKind == JsonValueKind.Number
            && actual.TryGetUInt64(out ulong a) && desired.TryGetUInt64(out ulong d) && a == d,
        RegistryValueType.Sz or RegistryValueType.ExpandSz =>
            actual.ValueKind == JsonValueKind.String && desired.ValueKind == JsonValueKind.String
            && string.Equals(actual.GetString(), desired.GetString(), StringComparison.Ordinal),
        RegistryValueType.MultiSz => JsonArrayOfStringsEquals(actual, desired),
        RegistryValueType.Binary => BinaryEquals(actual, desired),
        _ => false,
    };

    private static bool BinaryEquals(JsonElement actual, JsonElement desired)
    {
        if (actual.ValueKind != JsonValueKind.String || desired.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return actual.TryGetBytesFromBase64(out byte[]? actualBytes)
            && desired.TryGetBytesFromBase64(out byte[]? desiredBytes)
            && actualBytes.AsSpan().SequenceEqual(desiredBytes);
    }

    private static bool JsonArrayOfStringsEquals(JsonElement actual, JsonElement desired)
    {
        if (actual.ValueKind != JsonValueKind.Array || desired.ValueKind != JsonValueKind.Array
            || actual.GetArrayLength() != desired.GetArrayLength())
        {
            return false;
        }

        for (int i = 0; i < actual.GetArrayLength(); i++)
        {
            if (!string.Equals(actual[i].GetString(), desired[i].GetString(), StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool JsonEquals(JsonElement actual, JsonElement desired)
    {
        if (actual.ValueKind != desired.ValueKind)
        {
            return false;
        }

        switch (actual.ValueKind)
        {
            case JsonValueKind.Number:
                return actual.TryGetDecimal(out decimal a) && desired.TryGetDecimal(out decimal d)
                    ? a == d
                    : string.Equals(actual.GetRawText(), desired.GetRawText(), StringComparison.Ordinal);
            case JsonValueKind.String:
                return string.Equals(actual.GetString(), desired.GetString(), StringComparison.Ordinal);
            case JsonValueKind.Array:
                if (actual.GetArrayLength() != desired.GetArrayLength())
                {
                    return false;
                }

                for (int i = 0; i < actual.GetArrayLength(); i++)
                {
                    if (!JsonEquals(actual[i], desired[i]))
                    {
                        return false;
                    }
                }

                return true;
            default:
                return string.Equals(actual.GetRawText(), desired.GetRawText(), StringComparison.Ordinal);
        }
    }
}
