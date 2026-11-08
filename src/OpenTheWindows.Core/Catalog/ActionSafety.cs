using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core.Catalog.Actions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.Core.Catalog;

/// <summary>
/// The single code-level safety decision for an action, shared by the catalogue
/// validator, the apply planner, the action applier and revert. It needs no
/// machine state: a protected-process (PPL) service check, which does, stays in the
/// applier. Apply-direction checks include the weakening rules; restore-direction
/// checks (rollback and revert) apply only the location rules, so a revert can put
/// back exactly what the machine had, but never into a code-execution hook.
/// </summary>
public static class ActionSafety
{
    /// <summary>Why applying <paramref name="action"/> is refused, or <see langword="null"/> when it is permitted.</summary>
    public static string? ApplyRefusal(ITweakAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action switch
        {
            ServiceAction s => ProtectedServices.IsProtected(s.Name)
                ? string.Create(CultureInfo.InvariantCulture, $"Service '{s.Name}' is protected and must not be reconfigured.")
                : null,
            RegistryAction r => RegistryRefusal(r, r.Value, restoring: false),
            ScheduledTaskAction t => ProtectedScheduledTasks.Refusal(t.Path, t.Enabled),
            CommandAction c => CommandRefusal(c),
            DefenderPreferenceAction d => DefenderSafety.RefusesDefender(d) ? DefenderRefusal(d.Property) : null,
            SecurityPolicyAction p => SecurityPolicyValues.Refusal(p.Setting, p.Value),
            _ => null,
        };
    }

    /// <summary>
    /// Why restoring <paramref name="prior"/> through <paramref name="action"/>'s
    /// target (rollback or revert) is refused, or <see langword="null"/> when it is permitted.
    /// </summary>
    public static string? RestoreRefusal(ITweakAction action, JournalActionState? prior)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action switch
        {
            ServiceAction s => ProtectedServices.IsProtected(s.Name)
                ? string.Create(CultureInfo.InvariantCulture, $"Service '{s.Name}' is protected and must not be reconfigured.")
                : null,
            RegistryAction r => RegistryRefusal(r, prior is { Exists: true, Data: { } data } ? data : null, restoring: true),
            ScheduledTaskAction t when prior is { Enabled: false } => ProtectedScheduledTasks.Refusal(t.Path, enabled: false),
            CommandAction c => CommandRefusal(c),
            DefenderPreferenceAction d when prior is { Data: { } value } =>
                DefenderSafety.RefusesDefender(d with { Value = value }) ? DefenderRefusal(d.Property) : null,
            SecurityPolicyAction p when prior is { SecurityValue: { } value } => SecurityPolicyValues.Refusal(p.Setting, value),
            _ => null,
        };
    }

    /// <summary>Why <paramref name="command"/> is refused: an executable off the allow-list or an argument vector outside its permitted shapes.</summary>
    public static string? CommandRefusal(CommandAction command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return CommandArgumentPolicy.Refusal(command.Executable, command.Arguments)
            ?? CommandArgumentPolicy.Refusal(command.Executable, command.RevertArguments);
    }

    private static string? RegistryRefusal(RegistryAction action, JsonElement? value, bool restoring)
    {
        if (ProtectedRegistryTargets.Refusal(action.Path, action.Name, value, restoring) is { } reason)
        {
            return reason;
        }

        return DefenderSafety.RefusesRegistry(action)
            ? string.Create(CultureInfo.InvariantCulture,
                $"Writing under '{DefenderSafety.SignatureUpdatesPathFragment}' would interfere with Defender security-intelligence updates.")
            : null;
    }

    private static string DefenderRefusal(string property)
        => string.Create(CultureInfo.InvariantCulture, $"Defender preference '{property}' would disable a protection and must not be changed.");
}
