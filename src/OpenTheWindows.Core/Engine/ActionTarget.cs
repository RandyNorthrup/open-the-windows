using System.Globalization;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;

namespace OpenTheWindows.Core.Engine;

/// <summary>
/// Produces the stable, human- and machine-readable target string an action
/// writes, shared by the plan display and the journal so both name the same
/// thing the same way.
/// </summary>
public static class ActionTarget
{
    /// <summary>Describes what <paramref name="action"/> targets, resolving user hives against <paramref name="userSid"/>.</summary>
    public static string Describe(ITweakAction action, string? userSid)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action switch
        {
            RegistryAction r => DescribeRegistry(r, userSid),
            ServiceAction s => string.Create(CultureInfo.InvariantCulture, $"Service:{s.Name}"),
            ScheduledTaskAction t => string.Create(CultureInfo.InvariantCulture, $"Task:{t.Path}"),
            AppxAction a => string.Create(CultureInfo.InvariantCulture, $"Appx:{a.PackageFamilyName}"),
            OptionalFeatureAction f => string.Create(CultureInfo.InvariantCulture, $"Feature:{f.Name}"),
            DefenderPreferenceAction d => string.Create(CultureInfo.InvariantCulture, $"Defender:{d.Property}"),
            PowerSettingAction p => string.Create(CultureInfo.InvariantCulture, $"Power:{p.SubgroupGuid:D}/{p.SettingGuid:D}"),
            CommandAction c => string.Create(CultureInfo.InvariantCulture, $"Command:{c.Executable} {string.Join(' ', c.Arguments)}"),
            _ => action.Kind,
        };
    }

    private static string DescribeRegistry(RegistryAction action, string? userSid)
    {
        string root = action.Hive == RegistryHive.LocalMachine
            ? "HKLM"
            : string.Create(CultureInfo.InvariantCulture, $@"HKU\{userSid ?? "?"}");
        return string.Create(CultureInfo.InvariantCulture, $@"{root}\{action.Path}!{action.Name}");
    }
}
