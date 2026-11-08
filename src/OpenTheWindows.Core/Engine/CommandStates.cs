using System.Globalization;
using System.Text.RegularExpressions;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Catalog.Actions;

namespace OpenTheWindows.Core.Engine;

/// <summary>
/// Captures, before a catalogue command runs, the state that command changes, and turns
/// that captured state back into the commands that restore it — so a revert puts back what
/// was there, not a fixed "off" (a static <c>auditpol … /success:disable</c> would turn off
/// auditing Windows does by default; a static <c>/hibernate on</c> would turn hibernation on
/// on a PC where it was off). Each permitted command shape has one encoding:
/// <list type="bullet">
/// <item><c>powercfg /setactive</c>: <c>scheme:&lt;guid&gt;</c> (the active scheme).</item>
/// <item><c>powercfg /hibernate …</c>: <c>hibernate:off</c>, <c>hibernate:on:full</c> or <c>hibernate:on:reduced</c>.</item>
/// <item><c>fsutil behavior set disable8dot3</c>: <c>8dot3:&lt;0-3&gt;</c>.</item>
/// <item><c>auditpol /set /subcategory:…</c>: <c>audit:&lt;0-3&gt;</c> (bit 1 success, bit 2 failure).</item>
/// </list>
/// A state that cannot be read (another shape, an unreadable value, a non-English auditpol)
/// is <see langword="null"/>, and the revert falls back to the entry's declared revert arguments.
/// </summary>
internal static partial class CommandStates
{
    /// <summary>The power-control key holding the hibernation state.</summary>
    public const string PowerPath = @"SYSTEM\CurrentControlSet\Control\Power";

    /// <summary>The file-system key holding the 8.3 short-name setting.</summary>
    public const string FileSystemPath = @"SYSTEM\CurrentControlSet\Control\FileSystem";

    /// <summary>Whether hibernation is on (DWORD).</summary>
    public const string HibernateEnabledValue = "HibernateEnabled";

    /// <summary>The hibernation file type (DWORD: 1 reduced, 2 full).</summary>
    public const string HiberFileTypeValue = "HiberFileType";

    /// <summary>The 8.3 short-name creation setting (DWORD 0-3).</summary>
    public const string Disable8Dot3Value = "NtfsDisable8dot3NameCreation";

    private const string SchemePrefix = "scheme:";
    private const string HibernateOff = "hibernate:off";
    private const string HibernateOnFull = "hibernate:on:full";
    private const string HibernateOnReduced = "hibernate:on:reduced";
    private const string ShortNamePrefix = "8dot3:";
    private const string AuditPrefix = "audit:";
    private const string SubcategoryPrefix = "/subcategory:";
    private const int ReducedHiberFile = 1;
    private const int AuditSuccess = 1;
    private const int AuditFailure = 2;
    private const int InclusionColumn = 4;
    private const int Max8Dot3Setting = 3;

    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);

    private static readonly Dictionary<string, int> InclusionSettings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["No Auditing"] = 0,
        ["Success"] = AuditSuccess,
        ["Failure"] = AuditFailure,
        ["Success and Failure"] = AuditSuccess | AuditFailure,
    };

    /// <summary>Reads the state <paramref name="action"/> is about to change, or <see langword="null"/> when it cannot.</summary>
    public static string? Capture(CommandAction action, IRegistryReader registry, ICommandRunner runner)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(runner);
        return Shape(action) switch
        {
            CommandShape.SetActiveScheme => ActiveScheme(runner),
            CommandShape.Hibernate => Hibernation(registry),
            CommandShape.ShortNames => Dword(registry, FileSystemPath, Disable8Dot3Value) is >= 0 and <= Max8Dot3Setting and var value
                ? string.Create(CultureInfo.InvariantCulture, $"{ShortNamePrefix}{value}")
                : null,
            CommandShape.Audit => AuditSetting(action, runner),
            _ => null,
        };
    }

    /// <summary>The commands that put <paramref name="state"/> back for <paramref name="action"/>, or <see langword="null"/> when the state is not one this shape writes.</summary>
    public static IReadOnlyList<IReadOnlyList<string>>? RestoreCommands(CommandAction action, string state)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(state);
        return Shape(action) switch
        {
            CommandShape.SetActiveScheme when state.StartsWith(SchemePrefix, StringComparison.Ordinal)
                && Guid.TryParseExact(state[SchemePrefix.Length..], "D", out Guid scheme)
                => [["/setactive", scheme.ToString("D")]],
            CommandShape.Hibernate => state switch
            {
                HibernateOff => [["/hibernate", "off"]],
                HibernateOnFull => [["/hibernate", "on"], ["/hibernate", "/type", "full"]],
                HibernateOnReduced => [["/hibernate", "on"], ["/hibernate", "/type", "reduced"]],
                _ => null,
            },
            CommandShape.ShortNames when state.StartsWith(ShortNamePrefix, StringComparison.Ordinal)
                && int.TryParse(state[ShortNamePrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value <= Max8Dot3Setting
                => [["behavior", "set", "disable8dot3", value.ToString(CultureInfo.InvariantCulture)]],
            CommandShape.Audit when state.StartsWith(AuditPrefix, StringComparison.Ordinal)
                && int.TryParse(state[AuditPrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out int flags) && flags <= (AuditSuccess | AuditFailure)
                => [["/set", action.Arguments[1], (flags & AuditSuccess) != 0 ? "/success:enable" : "/success:disable", (flags & AuditFailure) != 0 ? "/failure:enable" : "/failure:disable"]],
            _ => null,
        };
    }

    private static CommandShape Shape(CommandAction action)
    {
        IReadOnlyList<string> args = action.Arguments;
        if (args.Count == 0 || CommandArgumentPolicy.Refusal(action.Executable, args) is not null)
        {
            return CommandShape.Other;
        }

        string verb = args[0].TrimStart('/', '-');
        return action.Executable.ToUpperInvariant() switch
        {
            "POWERCFG.EXE" when string.Equals(verb, "setactive", StringComparison.OrdinalIgnoreCase) => CommandShape.SetActiveScheme,
            "POWERCFG.EXE" when verb.Equals("hibernate", StringComparison.OrdinalIgnoreCase) || verb.Equals("h", StringComparison.OrdinalIgnoreCase) => CommandShape.Hibernate,
            "FSUTIL.EXE" => CommandShape.ShortNames,
            "AUDITPOL.EXE" => CommandShape.Audit,
            _ => CommandShape.Other,
        };
    }

    private static string? ActiveScheme(ICommandRunner runner)
    {
        CommandResult result = runner.Run("powercfg.exe", ["/getactivescheme"], ReadTimeout);
        Match match = SchemeGuid().Match(result.StandardOutput);
        return !result.TimedOut && result.ExitCode == 0 && match.Success && Guid.TryParse(match.Value, out Guid scheme)
            ? SchemePrefix + scheme.ToString("D")
            : null;
    }

    private static string? Hibernation(IRegistryReader registry)
    {
        int? enabled = Dword(registry, PowerPath, HibernateEnabledValue);
        if (enabled is null)
        {
            return null;
        }

        if (enabled == 0)
        {
            return HibernateOff;
        }

        return Dword(registry, PowerPath, HiberFileTypeValue) == ReducedHiberFile ? HibernateOnReduced : HibernateOnFull;
    }

    private static string? AuditSetting(CommandAction action, ICommandRunner runner)
    {
        string subcategory = action.Arguments[1];
        if (!subcategory.StartsWith(SubcategoryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        CommandResult result = runner.Run("auditpol.exe", ["/get", subcategory, "/r"], ReadTimeout);
        if (result.TimedOut || result.ExitCode != 0)
        {
            return null;
        }

        // The /r report is CSV: a header, then one row per subcategory; column 5 is the inclusion setting.
        string? row = result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Skip(1).FirstOrDefault();
        string[] columns = row?.Split(',') ?? [];
        return columns.Length > InclusionColumn && InclusionSettings.TryGetValue(columns[InclusionColumn].Trim(), out int flags)
            ? string.Create(CultureInfo.InvariantCulture, $"{AuditPrefix}{flags}")
            : null;
    }

    private static int? Dword(IRegistryReader registry, string path, string name)
        => registry.Read(RegistryHive.LocalMachine, null, path, name) is { Exists: true, Type: RegistryValueType.Dword, Data: { } data }
            && data.TryGetInt32(out int value)
            ? value
            : null;

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex SchemeGuid();

    private enum CommandShape
    {
        Other,
        SetActiveScheme,
        Hibernate,
        ShortNames,
        Audit,
    }
}
