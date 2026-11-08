using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OpenTheWindows.Core.Catalog;

/// <summary>
/// Registry locations the engine never writes or deletes, whatever a catalogue,
/// profile or journal asks for. Two kinds of rule apply:
/// <list type="bullet">
/// <item>Location rules refuse any write <em>or delete</em> (apply, rollback and
/// revert alike) of code-execution and persistence hooks (IFEO, Run keys, Winlogon
/// shell, LSA packages, COM server paths, service binaries) and of the start
/// configuration of <see cref="ProtectedServices"/>.</item>
/// <item>Weakening rules refuse only the apply direction of a value that turns a
/// security control off (UAC, Defender, SmartScreen, firewall, Windows Update
/// access). Restoring a captured prior is not blocked by them, so a revert can put
/// back exactly what the machine had before.</item>
/// </list>
/// These are a code-level backstop beneath the catalogue's own review: they hold
/// for an operator <c>--catalog-dir</c> and for a journal replayed by revert.
/// </summary>
public static class ProtectedRegistryTargets
{
    private const string SystemPrefix = @"SYSTEM\";
    private const string NumberedControlSetStem = "ControlSet";
    private const string CurrentControlSet = "CurrentControlSet";
    private const int ControlSetDigits = 3;
    private const string ServicesPrefix = @"SYSTEM\CurrentControlSet\Services\";
    private const string SystemPolicies = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
    private const string DefenderPolicies = @"SOFTWARE\Policies\Microsoft\Windows Defender";
    private const string WindowsUpdatePolicies = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
    private const string Winlogon = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
    private const string WindowsNtWindows = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows";
    private const string Lsa = @"SYSTEM\CurrentControlSet\Control\Lsa";
    private const string SessionManager = @"SYSTEM\CurrentControlSet\Control\Session Manager";
    private const string EnableFirewall = "EnableFirewall";
    private const string SmartScreenEnabled = "SmartScreenEnabled";
    private const string SecuritySubkey = "Security";
    private const string ParametersSubkey = "Parameters";
    private const string ShellVerbSegment = "shell";
    private const string CommandSegment = "command";
    private const string DdeExecSegment = "ddeexec";
    private const string ShellOff = "Off";

    /// <summary>Subtrees (relative to the hive) under which every write and delete is refused.</summary>
    private static readonly string[] ForbiddenSubtrees =
    [
        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion\Image File Execution Options",
        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SilentProcessExit",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnceEx",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunServices",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunServicesOnce",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer\Run",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects",
        @"SOFTWARE\Microsoft\Windows Defender\Exclusions",
        @"SOFTWARE\Microsoft\Windows Defender\Features",
        @"SOFTWARE\Microsoft\Windows Defender\Signature Updates",
        DefenderPolicies + @"\Exclusions",
        DefenderPolicies + @"\Signature Updates",
        SessionManager + @"\KnownDLLs",
        SessionManager + @"\AppCertDlls",
        @"SYSTEM\CurrentControlSet\Control\Print\Monitors",
        Winlogon + @"\Notify",
        Winlogon + @"\GPExtensions",
        @"SOFTWARE\Microsoft\Cryptography\OID",
        @"SOFTWARE\Microsoft\Cryptography\Providers\Trust",
    ];

    /// <summary>Individual values every write and delete of which is refused.</summary>
    private static readonly (string Key, string Name)[] ForbiddenValues =
    [
        (Winlogon, "Shell"),
        (Winlogon, "Userinit"),
        (Winlogon, "Taskman"),
        (Winlogon, "AppSetup"),
        (Winlogon, "GinaDLL"),
        (Winlogon, "VmApplet"),
        (Winlogon, "System"),
        (WindowsNtWindows, "AppInit_DLLs"),
        (WindowsNtWindows, "LoadAppInit_DLLs"),
        (WindowsNtWindows, "IconServiceLib"),
        (Lsa, "Security Packages"),
        (Lsa, "Authentication Packages"),
        (Lsa, "Notification Packages"),
        (Lsa + @"\OSConfig", "Security Packages"),
        (SessionManager, "BootExecute"),
        (SessionManager, "SetupExecute"),
        (SessionManager, "Execute"),
        (SessionManager, "S0InitialCommand"),
        (ServicesPrefix + @"Tcpip\Parameters", "DataBasePath"),
        (WindowsUpdatePolicies, "WUServer"),
        (WindowsUpdatePolicies, "WUStatusServer"),
        (WindowsUpdatePolicies, "UpdateServiceUrlAlternate"),
    ];

    /// <summary>Service values that change which binary runs or under which account: refused for every service.</summary>
    private static readonly HashSet<string> ServiceIdentityValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "ImagePath", "ServiceDll", "ObjectName", "FailureCommand", "FailureActions", "RequiredPrivileges",
    };

    /// <summary>Service values that change whether a service starts: refused for <see cref="ProtectedServices"/>.</summary>
    private static readonly HashSet<string> ServiceStartValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "Start", "DelayedAutostart", "Type",
    };

    /// <summary>COM registration subkeys whose default value names the code a COM activation loads.</summary>
    private static readonly HashSet<string> ComServerSubkeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "InprocServer32", "LocalServer32", "InprocHandler32", "TreatAs",
    };

    /// <summary>Apply-direction rules: a value under a key prefix that would switch a protection off.</summary>
    private static readonly WeakeningRule[] WeakeningRules =
    [
        new(SystemPolicies, "EnableLUA", IsZero, "turns off User Account Control"),
        new(SystemPolicies, "ConsentPromptBehaviorAdmin", IsZero, "elevates administrators without any prompt"),
        new(SystemPolicies, "PromptOnSecureDesktop", IsZero, "moves the elevation prompt off the secure desktop"),
        new(SystemPolicies, "LocalAccountTokenFilterPolicy", IsNonZero, "gives remote local-account logons a full administrator token"),
        new(DefenderPolicies, "DisableAntiSpyware", IsNonZero, "turns off Microsoft Defender"),
        new(DefenderPolicies, "DisableAntiVirus", IsNonZero, "turns off Microsoft Defender Antivirus"),
        new(DefenderPolicies, "DisableRealtimeMonitoring", IsNonZero, "turns off real-time protection"),
        new(DefenderPolicies, "DisableBehaviorMonitoring", IsNonZero, "turns off behaviour monitoring"),
        new(DefenderPolicies, "DisableIOAVProtection", IsNonZero, "stops scanning of downloaded files"),
        new(DefenderPolicies, "DisableOnAccessProtection", IsNonZero, "turns off on-access protection"),
        new(DefenderPolicies, "DisableScriptScanning", IsNonZero, "turns off script scanning"),
        new(DefenderPolicies, "DisableRoutinelyTakingAction", IsNonZero, "stops Defender acting on detected threats"),
        new(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableSmartScreen", IsZero, "turns off SmartScreen for apps and files"),
        new(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", SmartScreenEnabled, IsOff, "turns off SmartScreen for apps and files"),
        new(@"SOFTWARE\Policies\Microsoft\Edge", SmartScreenEnabled, IsZero, "turns off Microsoft Edge SmartScreen"),
        new(@"SOFTWARE\Policies\Microsoft\WindowsFirewall", EnableFirewall, IsZero, "turns off Windows Firewall"),
        new(ServicesPrefix + @"SharedAccess\Parameters\FirewallPolicy", EnableFirewall, IsZero, "turns off Windows Firewall"),
        new(WindowsUpdatePolicies, "DisableWindowsUpdateAccess", IsNonZero, "blocks all access to Windows Update"),
        new(WindowsUpdatePolicies, "SetDisableUXWUAccess", IsNonZero, "removes the user's access to Windows Update"),
    ];

    /// <summary>
    /// Returns why writing (or, when <paramref name="value"/> is <see langword="null"/>,
    /// deleting) <paramref name="name"/> under <paramref name="path"/> is refused, or
    /// <see langword="null"/> when it is permitted. <paramref name="restoring"/> is
    /// <see langword="true"/> for rollback and revert, which skip the weakening rules.
    /// </summary>
    /// <param name="path">The key path relative to its hive (machine or user).</param>
    /// <param name="name">The value name (empty for the default value).</param>
    /// <param name="value">The data to write, or <see langword="null"/> for a delete.</param>
    /// <param name="restoring">Whether this write restores a previously captured state.</param>
    public static string? Refusal(string path, string name, JsonElement? value, bool restoring)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(name);

        string key = Normalize(path);
        string? location = LocationRefusal(key, name, value);
        if (location is not null || restoring || value is not { } data)
        {
            return location;
        }

        WeakeningRule? weakening = Array.Find(WeakeningRules, rule =>
            IsUnder(key, rule.KeyPrefix) && string.Equals(name, rule.Name, StringComparison.OrdinalIgnoreCase) && rule.IsWeakening(data));
        return weakening is null
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"Setting '{name}' under '{path}' {weakening.Reason}; this product never weakens that control.");
    }

    private static string? LocationRefusal(string key, string name, JsonElement? value)
    {
        if (ForbiddenSubtrees.Any(subtree => IsUnder(key, subtree))
            || ForbiddenValues.Any(v => string.Equals(key, v.Key, StringComparison.OrdinalIgnoreCase) && string.Equals(name, v.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return string.Create(CultureInfo.InvariantCulture, $"'{key}\\{name}' is a code-execution or persistence location and is never written.");
        }

        if (ServiceRefusal(key, name) is { } service)
        {
            return service;
        }

        return IsComCodePath(key) && !IsEmptyString(value)
            ? string.Create(CultureInfo.InvariantCulture, $"'{key}' names the code a COM or shell activation runs and is never pointed at a binary.")
            : null;
    }

    private static string? ServiceRefusal(string key, string name)
    {
        if (!key.StartsWith(ServicesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string[] segments = key[ServicesPrefix.Length..].Split('\\');
        string service = segments[0];
        bool serviceRoot = segments.Length == 1;
        bool parameters = segments.Length == 2 && string.Equals(segments[1], ParametersSubkey, StringComparison.OrdinalIgnoreCase);

        if (segments.Length >= 2 && string.Equals(segments[1], SecuritySubkey, StringComparison.OrdinalIgnoreCase))
        {
            return string.Create(CultureInfo.InvariantCulture, $"The security descriptor of service '{service}' is never changed.");
        }

        if ((serviceRoot || parameters) && ServiceIdentityValues.Contains(name))
        {
            return string.Create(CultureInfo.InvariantCulture, $"'{name}' of service '{service}' controls what the service runs and is never changed.");
        }

        return serviceRoot && ServiceStartValues.Contains(name) && ProtectedServices.IsProtected(service)
            ? string.Create(CultureInfo.InvariantCulture, $"Service '{service}' is protected and its start configuration is never changed.")
            : null;
    }

    private static bool IsComCodePath(string key)
    {
        int classes = key.IndexOf(@"\Classes\", StringComparison.OrdinalIgnoreCase);
        if (classes < 0 && !key.StartsWith(@"Classes\", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] segments = key.Split('\\');
        string last = segments[^1];
        if (ComServerSubkeys.Contains(last))
        {
            return true;
        }

        bool shellCommand = string.Equals(last, CommandSegment, StringComparison.OrdinalIgnoreCase)
            || string.Equals(last, DdeExecSegment, StringComparison.OrdinalIgnoreCase);
        return shellCommand && segments.Any(s => string.Equals(s, ShellVerbSegment, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Canonicalises a key path: collapses repeated separators, trims leading and
    /// trailing separators, and maps <c>ControlSet00n</c> to <c>CurrentControlSet</c>,
    /// so a rule cannot be dodged by spelling the same key another way.
    /// </summary>
    internal static string Normalize(string path)
    {
        var builder = new StringBuilder(path.Length);
        foreach (char c in path.Replace('/', '\\'))
        {
            if (c == '\\' && builder.Length > 0 && builder[^1] == '\\')
            {
                continue;
            }

            builder.Append(c);
        }

        string collapsed = builder.ToString().Trim('\\');
        if (!collapsed.StartsWith(SystemPrefix + NumberedControlSetStem, StringComparison.OrdinalIgnoreCase))
        {
            return collapsed;
        }

        int end = collapsed.IndexOf('\\', SystemPrefix.Length);
        string segment = end < 0 ? collapsed[SystemPrefix.Length..] : collapsed[SystemPrefix.Length..end];
        bool numbered = segment.Length == NumberedControlSetStem.Length + ControlSetDigits
            && segment[NumberedControlSetStem.Length..].All(char.IsAsciiDigit);
        if (!numbered)
        {
            return collapsed;
        }

        string rest = end < 0 ? string.Empty : collapsed[end..];
        return string.Concat(SystemPrefix, CurrentControlSet, rest);
    }

    private static bool IsUnder(string key, string prefix)
        => key.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || (key.Length > prefix.Length
                && key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && key[prefix.Length] == '\\');

    private static bool IsEmptyString(JsonElement? value)
        => value is null || (value.Value.ValueKind == JsonValueKind.String && string.IsNullOrEmpty(value.Value.GetString()));

    private static bool IsZero(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.TryGetInt64(out long number) && number == 0,
        JsonValueKind.String => string.Equals(value.GetString()?.Trim(), "0", StringComparison.Ordinal),
        JsonValueKind.False => true,
        _ => false,
    };

    private static bool IsNonZero(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => !value.TryGetInt64(out long number) || number != 0,
        JsonValueKind.String => !string.Equals(value.GetString()?.Trim(), "0", StringComparison.Ordinal),
        JsonValueKind.True => true,
        _ => false,
    };

    private static bool IsOff(JsonElement value)
        => value.ValueKind == JsonValueKind.String && string.Equals(value.GetString()?.Trim(), ShellOff, StringComparison.OrdinalIgnoreCase);

    private sealed record WeakeningRule(string KeyPrefix, string Name, Func<JsonElement, bool> IsWeakening, string Reason);
}
