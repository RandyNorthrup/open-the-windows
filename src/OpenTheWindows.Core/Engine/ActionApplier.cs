using System.Globalization;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.Core.Engine;

/// <summary>
/// Executes a single action of any catalogue kind against the machine: it
/// captures the prior state for the journal, applies the desired state, verifies
/// the result by re-reading, and restores a captured prior on revert/rollback.
/// Code-level refusals (protected/PPL services, disallowed executables) are
/// enforced here regardless of what the catalogue asked for.
/// </summary>
public sealed class ActionApplier
{
    private static readonly TimeSpan ServiceStopTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(2);

    private readonly StateReaders _readers;
    private readonly StateWriters _writers;

    /// <summary>Creates the applier over the given readers and writers.</summary>
    public ActionApplier(StateReaders readers, StateWriters writers)
    {
        _readers = readers ?? throw new ArgumentNullException(nameof(readers));
        _writers = writers ?? throw new ArgumentNullException(nameof(writers));
    }

    /// <summary>
    /// Returns a human-readable reason when a code-level safety rule refuses the
    /// action, or <see langword="null"/> when it is permitted. Used at plan time
    /// so a refused entry is never partially applied.
    /// </summary>
    public string? CheckRefusal(ITweakAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action is ServiceAction service ? ServiceRefusal(service.Name) : ActionSafety.ApplyRefusal(action);
    }

    /// <summary>
    /// Returns why restoring <paramref name="prior"/> through <paramref name="action"/>
    /// (rollback or revert) is refused, or <see langword="null"/> when it is permitted.
    /// Revert checks every action with this before it changes anything, so a journal
    /// can never drive a write into a protected location.
    /// </summary>
    public static string? CheckRestoreRefusal(ITweakAction action, JournalActionState? prior)
    {
        ArgumentNullException.ThrowIfNull(action);
        return ActionSafety.RestoreRefusal(action, prior);
    }

    /// <summary>Reads the current on-machine state for the journal's <c>prior</c>; <see langword="null"/> for kinds with no revertible prior.</summary>
    public JournalActionState? CapturePrior(ITweakAction action, string? userSid)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action switch
        {
            RegistryAction r => CaptureRegistry(r, userSid),
            ServiceAction s => CaptureService(s),
            ScheduledTaskAction t => CaptureTask(t),
            AppxAction a => CaptureAppx(a, userSid),
            OptionalFeatureAction f => CaptureFeature(f),
            DefenderPreferenceAction d => CaptureDefender(d),
            PowerSettingAction p => CapturePower(p),
            SecurityPolicyAction s => CaptureSecurityPolicy(s),
            CommandAction => null,
            _ => null,
        };
    }

    /// <summary>
    /// Whether the action would overwrite an existing registry value whose type the
    /// reader cannot represent (for example <c>REG_NONE</c> or <c>REG_RESOURCE_LIST</c>).
    /// Such a value cannot be captured for revert — restoring it would delete it — so
    /// the planner refuses the action instead of destroying the original.
    /// </summary>
    public bool HasUncapturablePrior(ITweakAction action, string? userSid)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action is RegistryAction r
            && (r.Hive != RegistryHive.User || userSid is not null)
            && _readers.Registry.Read(r.Hive, HiveSid(r.Hive, userSid), r.Path, r.Name) is { Exists: true, Type: null };
    }

    /// <summary>The desired state as a journal state, for the journal's <c>desired</c>.</summary>
    public static JournalActionState DescribeDesired(ITweakAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action switch
        {
            RegistryAction r => new JournalActionState { Type = r.Type, Data = r.Value },
            ServiceAction s => new JournalActionState { StartType = s.StartType },
            ScheduledTaskAction t => new JournalActionState { Enabled = t.Enabled },
            AppxAction a => new JournalActionState { InstalledForUser = false, Deprovisioned = a.Mode == AppxRemovalMode.AllUsersAndDeprovision },
            OptionalFeatureAction f => new JournalActionState { Enabled = f.Enabled },
            DefenderPreferenceAction d => new JournalActionState { Data = d.Value },
            PowerSettingAction p => new JournalActionState { Ac = p.AcValue, Dc = p.DcValue },
            SecurityPolicyAction s => new JournalActionState { SecurityValue = s.Value },
            _ => new JournalActionState(),
        };
    }

    /// <summary>
    /// Whether an action writes to a user's hive (a user-hive registry action or a
    /// per-user Appx removal) rather than machine-wide state. Such actions are the
    /// ones an all-users apply repeats across every user hive.
    /// </summary>
    public static bool IsUserScoped(ITweakAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action is RegistryAction { Hive: RegistryHive.User } or AppxAction;
    }

    /// <summary>
    /// Whether every one of <paramref name="entry"/>'s actions writes only a user's
    /// hive, so the entry can be applied without elevation to the calling user's own
    /// hive. An entry with no actions, or with any machine-scope action, is not
    /// user-scoped. This is the filter a <c>remediate --user-scope</c> run applies so
    /// a non-elevated caller never attempts a machine write.
    /// </summary>
    public static bool IsUserScoped(TweakDefinition entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Actions.Count > 0 && entry.Actions.All(IsUserScoped);
    }

    /// <summary>
    /// Whether the action cannot run without a specific user's SID: a user-hive registry
    /// value or a current-user app removal. An all-users app removal deprovisions the
    /// package machine-wide and needs no user.
    /// </summary>
    public static bool RequiresUser(ITweakAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action is RegistryAction { Hive: RegistryHive.User } or AppxAction { Mode: AppxRemovalMode.CurrentUser };
    }

    /// <summary>
    /// The user SID an action actually writes under: the target user's SID for a
    /// user-scoped action, and <see langword="null"/> for a machine-scope action
    /// that ignores the SID. Journaled per action so revert and rollback restore
    /// the same hive the action wrote.
    /// </summary>
    public static string? EffectiveSid(ITweakAction action, string? userSid)
        => IsUserScoped(action) ? userSid : null;

    /// <summary>Applies the action's desired state; throws <see cref="RefusedOperationException"/> on a refusal and on writer failure.</summary>
    public void Apply(ITweakAction action, string? userSid)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (ActionSafety.ApplyRefusal(action) is { } refusal)
        {
            throw new RefusedOperationException(refusal);
        }

        switch (action)
        {
            case RegistryAction r:
                _writers.Registry.Write(r.Hive, HiveSid(r.Hive, userSid), r.Path, r.Name, r.Type, r.Value, r.ValueKind);
                break;
            case ServiceAction s:
                ApplyService(s);
                break;
            case ScheduledTaskAction t:
                _writers.Tasks.SetEnabled(t.Path, t.Enabled);
                break;
            case AppxAction a:
                _writers.Appx.Remove(a.PackageFamilyName, userSid, a.Mode);
                break;
            case OptionalFeatureAction f:
                _writers.Features.SetEnabled(f.Name, f.Enabled);
                break;
            case DefenderPreferenceAction d:
                _writers.Defender.SetPreference(d.Property, d.Value);
                break;
            case PowerSettingAction p:
                _writers.Power.SetValues(p.SubgroupGuid, p.SettingGuid, p.AcValue, p.DcValue);
                break;
            case SecurityPolicyAction s:
                _writers.SecurityPolicy.Configure(s.Section, s.Setting, s.Value);
                break;
            case CommandAction c:
                RunCommand(c, c.Arguments);
                break;
            default:
                throw new RefusedOperationException(
                    string.Create(CultureInfo.InvariantCulture, $"Unsupported action kind '{action.Kind}'."));
        }
    }

    /// <summary>Re-reads the machine and reports whether the applied state matches the desired state.</summary>
    public bool Verify(ITweakAction action, string? userSid)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action switch
        {
            RegistryAction r => VerifyRegistry(r, userSid),
            ServiceAction s => _readers.Services.Read(s.Name) is { } snap && snap.StartType == s.StartType,
            ScheduledTaskAction t => _readers.Tasks.Read(t.Path) is { Exists: true } snap && snap.Enabled == t.Enabled,
            AppxAction a => VerifyAppx(a, userSid),
            OptionalFeatureAction f => _readers.Features.IsEnabled(f.Name) == f.Enabled,
            DefenderPreferenceAction d => _readers.Defender.Read(d.Property) is { } value && StateComparison.JsonEquals(value, d.Value),
            PowerSettingAction p => _readers.Power.Read(p.SubgroupGuid, p.SettingGuid) is { } read && read.Ac == p.AcValue && read.Dc == p.DcValue,
            SecurityPolicyAction s => string.Equals(_readers.SecurityPolicy.Read(s.Section, s.Setting), s.Value, StringComparison.Ordinal),
            CommandAction => true,
            _ => false,
        };
    }

    /// <summary>Re-reads the machine and reports whether it now matches a restored <paramref name="prior"/> state (including "absent").</summary>
    public bool VerifyRestored(ITweakAction action, JournalActionState? prior, string? userSid)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action switch
        {
            RegistryAction r => VerifyRegistryRestored(r, prior, userSid),
            ServiceAction s => prior?.StartType is not { } startType || (_readers.Services.Read(s.Name) is { } snap && snap.StartType == startType),
            ScheduledTaskAction t => prior?.Enabled is not { } enabled || (_readers.Tasks.Read(t.Path) is { Exists: true } snap && snap.Enabled == enabled),
            OptionalFeatureAction f => prior?.Enabled is not { } enabled || _readers.Features.IsEnabled(f.Name) == enabled,
            DefenderPreferenceAction d => prior?.Data is not { } value || (_readers.Defender.Read(d.Property) is { } read && StateComparison.JsonEquals(read, value)),
            PowerSettingAction p => prior is not { Ac: { } ac, Dc: { } dc } || (_readers.Power.Read(p.SubgroupGuid, p.SettingGuid) is { } read && read.Ac == ac && read.Dc == dc),
            SecurityPolicyAction s => prior?.SecurityValue is not { } value || string.Equals(_readers.SecurityPolicy.Read(s.Section, s.Setting), value, StringComparison.Ordinal),
            // Appx reprovision cannot be verified by a subsequent read (staging is asynchronous); trust the writer.
            AppxAction => true,
            CommandAction => true,
            _ => false,
        };
    }

    /// <summary>
    /// Whether <see cref="Restore"/> can put <paramref name="prior"/> back for
    /// <paramref name="action"/>. A removed app package (its files are gone) and a
    /// setting whose prior value could not be read cannot be restored; rollback and
    /// revert report those as not reversible instead of claiming success.
    /// </summary>
    public static bool CanRestore(ITweakAction action, JournalActionState? prior)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action switch
        {
            RegistryAction or CommandAction => true,
            ServiceAction => prior is { StartType: not null },
            ScheduledTaskAction => prior is { Exists: true, Enabled: not null },
            AppxAction => prior is { InstalledForAnyUser: false },
            OptionalFeatureAction => prior is { Exists: true, Enabled: not null },
            DefenderPreferenceAction => prior is { Data: not null },
            PowerSettingAction => prior is { Ac: not null, Dc: not null },
            SecurityPolicyAction => prior is { SecurityValue: not null },
            _ => false,
        };
    }

    /// <summary>Restores a captured prior state (revert/rollback). For commands this runs the revert arguments.</summary>
    public void Restore(ITweakAction action, JournalActionState? prior, string? userSid)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (ActionSafety.RestoreRefusal(action, prior) is { } refusal)
        {
            throw new RefusedOperationException(refusal);
        }

        switch (action)
        {
            case RegistryAction r:
                RestoreRegistry(r, prior, userSid);
                break;
            case ServiceAction s when prior is { StartType: { } startType }:
                RestoreService(s.Name, startType, prior.Running == true);
                break;
            case ScheduledTaskAction t when prior is { Exists: true, Enabled: { } enabled }:
                _writers.Tasks.SetEnabled(t.Path, enabled);
                break;
            case AppxAction { Mode: AppxRemovalMode.AllUsersAndDeprovision } a when prior is { Deprovisioned: false }:
                // Reprovisioning restores an all-users deprovision so new users receive the app again.
                // A per-user removal is not reversible here because the package bits are gone (the catalogue
                // entry carries a reinstall hint for that case).
                _writers.Appx.Reprovision(a.PackageFamilyName);
                break;
            case OptionalFeatureAction f when prior is { Exists: true, Enabled: { } enabled }:
                _writers.Features.SetEnabled(f.Name, enabled);
                break;
            case DefenderPreferenceAction d when prior is { Data: { } value }:
                _writers.Defender.SetPreference(d.Property, value);
                break;
            case PowerSettingAction p when prior is { Ac: { } ac, Dc: { } dc }:
                _writers.Power.SetValues(p.SubgroupGuid, p.SettingGuid, ac, dc);
                break;
            case SecurityPolicyAction s when prior is { SecurityValue: { } value }:
                _writers.SecurityPolicy.Configure(s.Section, s.Setting, value);
                break;
            case CommandAction c:
                RunCommand(c, c.RevertArguments);
                break;
            default:
                // Nothing revertible (e.g. a service/task whose prior did not exist); leave as-is.
                break;
        }
    }

    private static string? HiveSid(RegistryHive hive, string? userSid) => hive == RegistryHive.User ? userSid : null;

    private string? ServiceRefusal(string name)
    {
        if (ProtectedServices.IsProtected(name))
        {
            return string.Create(CultureInfo.InvariantCulture, $"Service '{name}' is protected and must not be reconfigured.");
        }

        if (_readers.Services.Read(name) is { IsProtectedProcess: true })
        {
            return string.Create(CultureInfo.InvariantCulture, $"Service '{name}' runs as a protected process (PPL) and must not be reconfigured.");
        }

        return null;
    }

    private void ApplyService(ServiceAction action)
    {
        ServiceSnapshot snapshot = _readers.Services.Read(action.Name)
            ?? throw new RefusedOperationException(
                string.Create(CultureInfo.InvariantCulture, $"Service '{action.Name}' does not exist."));

        if (ProtectedServices.IsProtected(action.Name) || snapshot.IsProtectedProcess)
        {
            throw new RefusedOperationException(
                string.Create(CultureInfo.InvariantCulture, $"Service '{action.Name}' is protected and must not be reconfigured."));
        }

        _writers.Services.SetStartType(action.Name, action.StartType);
        if (action.StopIfRunning && snapshot.IsRunning)
        {
            _writers.Services.StopService(action.Name, ServiceStopTimeout);
        }
    }

    /// <summary>
    /// Restores a service's start type and, when it was running before the change
    /// (an apply with <c>stopIfRunning</c> stopped it), starts it again so a revert
    /// does not leave the service down until the next reboot.
    /// </summary>
    private void RestoreService(string name, ServiceStartType startType, bool wasRunning)
    {
        _writers.Services.SetStartType(name, startType);
        if (wasRunning && startType != ServiceStartType.Disabled && _readers.Services.Read(name) is { IsRunning: false })
        {
            _writers.Services.StartService(name, ServiceStopTimeout);
        }
    }

    private void RunCommand(CommandAction action, IReadOnlyList<string> arguments)
    {
        if (CommandArgumentPolicy.Refusal(action.Executable, arguments) is { } refusal)
        {
            throw new RefusedOperationException(refusal);
        }

        CommandResult result = _writers.Commands.Run(action.Executable, arguments, CommandTimeout);
        if (result.TimedOut || result.ExitCode != 0)
        {
            throw new RefusedOperationException(
                string.Create(CultureInfo.InvariantCulture,
                    $"Command '{action.Executable}' failed (exit {result.ExitCode}{(result.TimedOut ? ", timed out" : string.Empty)})."));
        }
    }

    private JournalActionState CaptureRegistry(RegistryAction action, string? userSid)
    {
        RegistryValueSnapshot snapshot = _readers.Registry.Read(action.Hive, HiveSid(action.Hive, userSid), action.Path, action.Name);
        return snapshot is { Exists: true }
            ? new JournalActionState { Exists = true, Type = snapshot.Type, Data = snapshot.Data }
            : new JournalActionState { Exists = false };
    }

    private JournalActionState CaptureService(ServiceAction action)
    {
        ServiceSnapshot? snapshot = _readers.Services.Read(action.Name);
        return snapshot is null
            ? new JournalActionState { Exists = false }
            : new JournalActionState { Exists = true, StartType = snapshot.StartType, Running = snapshot.IsRunning };
    }

    private JournalActionState CaptureTask(ScheduledTaskAction action)
    {
        ScheduledTaskSnapshot? snapshot = _readers.Tasks.Read(action.Path);
        return snapshot is { Exists: true }
            ? new JournalActionState { Exists = true, Enabled = snapshot.Enabled }
            : new JournalActionState { Exists = false };
    }

    private JournalActionState CaptureAppx(AppxAction action, string? userSid)
    {
        AppxSnapshot snapshot = _readers.Appx.Read(action.PackageFamilyName, userSid);
        return new JournalActionState
        {
            Exists = snapshot.InstalledForAnyUser,
            InstalledForUser = snapshot.InstalledForUser,
            InstalledForAnyUser = snapshot.InstalledForAnyUser,
            Deprovisioned = snapshot.Deprovisioned,
        };
    }

    private JournalActionState CaptureFeature(OptionalFeatureAction action)
    {
        bool? enabled = _readers.Features.IsEnabled(action.Name);
        return enabled is { } value
            ? new JournalActionState { Exists = true, Enabled = value }
            : new JournalActionState { Exists = false };
    }

    private JournalActionState CaptureDefender(DefenderPreferenceAction action)
    {
        System.Text.Json.JsonElement? value = _readers.Defender.Read(action.Property);
        return value is { } data
            ? new JournalActionState { Exists = true, Data = data }
            : new JournalActionState { Exists = false };
    }

    private JournalActionState CapturePower(PowerSettingAction action)
    {
        (uint Ac, uint Dc)? read = _readers.Power.Read(action.SubgroupGuid, action.SettingGuid);
        return read is { } value
            ? new JournalActionState { Exists = true, Ac = value.Ac, Dc = value.Dc }
            : new JournalActionState { Exists = false };
    }

    private JournalActionState CaptureSecurityPolicy(SecurityPolicyAction action)
    {
        string? value = _readers.SecurityPolicy.Read(action.Section, action.Setting);
        return value is null
            ? new JournalActionState { Exists = false }
            : new JournalActionState { Exists = true, SecurityValue = value };
    }

    private bool VerifyRegistry(RegistryAction action, string? userSid)
    {
        RegistryValueSnapshot snapshot = _readers.Registry.Read(action.Hive, HiveSid(action.Hive, userSid), action.Path, action.Name);
        return snapshot is { Exists: true, Type: { } type, Data: { } data }
            && type == action.Type
            && StateComparison.RegistryEquals(data, action.Value, action.Type);
    }

    private bool VerifyAppx(AppxAction action, string? userSid)
        => StateComparison.AppxCompliant(_readers.Appx.Read(action.PackageFamilyName, userSid), action.Mode);

    private bool VerifyRegistryRestored(RegistryAction action, JournalActionState? prior, string? userSid)
    {
        RegistryValueSnapshot snapshot = _readers.Registry.Read(action.Hive, HiveSid(action.Hive, userSid), action.Path, action.Name);
        if (prior is not { Exists: true, Type: { } type, Data: { } data })
        {
            // The prior state was "absent"; a correct restore leaves the value absent.
            return !snapshot.Exists;
        }

        return snapshot is { Exists: true, Type: { } actualType, Data: { } actualData }
            && actualType == type
            && StateComparison.RegistryEquals(actualData, data, type);
    }

    private void RestoreRegistry(RegistryAction action, JournalActionState? prior, string? userSid)
    {
        string? sid = HiveSid(action.Hive, userSid);
        if (prior is { Exists: true, Type: { } type, Data: { } data })
        {
            _writers.Registry.Write(action.Hive, sid, action.Path, action.Name, type, data, action.ValueKind);
        }
        else
        {
            _writers.Registry.Delete(action.Hive, sid, action.Path, action.Name, action.ValueKind);
        }
    }
}
