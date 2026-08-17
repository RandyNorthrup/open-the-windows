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
        return action switch
        {
            ServiceAction s => ServiceRefusal(s.Name),
            CommandAction c => CommandAction.AllowedExecutables.Contains(c.Executable)
                ? null
                : string.Create(CultureInfo.InvariantCulture, $"Executable '{c.Executable}' is not on the command allow-list."),
            _ => null,
        };
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
            CommandAction => null,
            _ => null,
        };
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
            _ => new JournalActionState(),
        };
    }

    /// <summary>Applies the action's desired state; throws <see cref="RefusedOperationException"/> on a refusal and on writer failure.</summary>
    public void Apply(ITweakAction action, string? userSid)
    {
        ArgumentNullException.ThrowIfNull(action);
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
            // Appx reprovision cannot be verified by a subsequent read (staging is asynchronous); trust the writer.
            AppxAction => true,
            CommandAction => true,
            _ => false,
        };
    }

    /// <summary>Restores a captured prior state (revert/rollback). For commands this runs the revert arguments.</summary>
    public void Restore(ITweakAction action, JournalActionState? prior, string? userSid)
    {
        ArgumentNullException.ThrowIfNull(action);
        switch (action)
        {
            case RegistryAction r:
                RestoreRegistry(r, prior, userSid);
                break;
            case ServiceAction s when prior is { StartType: { } startType }:
                _writers.Services.SetStartType(s.Name, startType);
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

    private void RunCommand(CommandAction action, IReadOnlyList<string> arguments)
    {
        if (!CommandAction.AllowedExecutables.Contains(action.Executable))
        {
            throw new RefusedOperationException(
                string.Create(CultureInfo.InvariantCulture, $"Executable '{action.Executable}' is not on the command allow-list."));
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
