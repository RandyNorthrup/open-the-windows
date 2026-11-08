using System.Globalization;
using System.Text.Json;
using OpenTheWindows.Core.Catalog.Actions;

namespace OpenTheWindows.Core.Catalog;

/// <summary>
/// One plain-language sentence per catalogue action, for people reading what an entry does
/// (the GUI's detail pane): the registry value and the value it is set to, the service and
/// its start type, the task, package, feature, preference, power setting, policy or command.
/// </summary>
public static class ActionText
{
    private const string MachineRoot = "HKLM";
    private const string UserRoot = "HKCU";

    /// <summary>Describes <paramref name="action"/> in one sentence.</summary>
    public static string Describe(ITweakAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action switch
        {
            RegistryAction r => Registry(r),
            ServiceAction s => s.StopIfRunning
                ? Text($"Set the {s.Name} service to start {StartType(s.StartType)}, and stop it if it is running")
                : Text($"Set the {s.Name} service to start {StartType(s.StartType)}"),
            ScheduledTaskAction t => Text($"{(t.Enabled ? "Enable" : "Disable")} the scheduled task {t.Path}"),
            AppxAction a => Text($"Remove the {a.PackageFamilyName} app ({Removal(a.Mode)})"),
            OptionalFeatureAction f => Text($"{(f.Enabled ? "Turn on" : "Turn off")} the Windows feature {f.Name}"),
            DefenderPreferenceAction d => Text($"Set the Microsoft Defender preference {d.Property} to {Value(d.Value)}"),
            PowerSettingAction p => Text($"Set power setting {p.SettingGuid:D} to {p.AcValue} when plugged in and {p.DcValue} on battery"),
            SecurityPolicyAction s => Text($"Set the local security policy {s.Setting} to {s.Value}"),
            CommandAction c => Text($"Run {c.Executable} {string.Join(' ', c.Arguments)}"),
            _ => action.Kind,
        };
    }

    private static string Registry(RegistryAction action)
    {
        string root = action.Hive == RegistryHive.LocalMachine ? MachineRoot : UserRoot;
        string kind = action.ValueKind == RegistryValueKind.Policy ? "policy" : "setting";
        string name = action.Name.Length == 0 ? "(default)" : action.Name;
        return Text($@"Set {root}\{action.Path}\{name} to {Value(action.Value)} ({action.Type} {kind})");
    }

    private static string StartType(ServiceStartType startType) => startType switch
    {
        ServiceStartType.Automatic => "automatically",
        ServiceStartType.AutomaticDelayed => "automatically (delayed)",
        ServiceStartType.Manual => "manually",
        ServiceStartType.Disabled => "never (disabled)",
        _ => startType.ToString(),
    };

    private static string Removal(AppxRemovalMode mode) => mode switch
    {
        AppxRemovalMode.AllUsersAndDeprovision => "for every user, and not for new users either",
        _ => "for the signed-in user",
    };

    private static string Value(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => Text($"\"{value.GetString()}\""),
        JsonValueKind.Array => List(value),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => value.GetRawText(),
    };

    private static string List(JsonElement array)
    {
        using JsonElement.ArrayEnumerator items = array.EnumerateArray();
        return Text($"[{string.Join(", ", items.Select(Value))}]");
    }

    private static string Text(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
