using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace OpenTheWindows.Windows.Readers;

/// <summary>
/// Read-only enumeration of registry values and subkeys under HKLM or a user's
/// <c>HKEY_USERS\&lt;SID&gt;</c> hive, always in the 64-bit view (a WOW6432Node path is just a
/// path). Never <c>HKEY_CURRENT_USER</c>: an elevated process may not be the signed-in user.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class RegistryEnumeration
{
    /// <summary>Opens <paramref name="path"/> under HKLM, or under <c>HKU\<paramref name="userSid"/></c> when a SID is given; null when absent or unreadable.</summary>
    public static RegistryKey? Open(string? userSid, string path)
    {
        if (userSid is null)
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            return TryOpenSubKey(machine, path);
        }

        using var users = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64);
        return TryOpenSubKey(users, string.Create(CultureInfo.InvariantCulture, $@"{userSid}\{path}"));
    }

    /// <summary>The named string values (REG_SZ / REG_EXPAND_SZ, unexpanded) under a key; empty when the key is absent.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> StringValues(string? userSid, string path)
    {
        using RegistryKey? key = Open(userSid, path);
        if (key is null)
        {
            return [];
        }

        List<KeyValuePair<string, string>> values = [];
        foreach (string name in key.GetValueNames().Where(n => n.Length > 0))
        {
            if (key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is string text)
            {
                values.Add(new(name, text));
            }
        }

        return values;
    }

    /// <summary>A QWORD value as a signed 64-bit number, or 0 when absent or of another type.</summary>
    public static long QWord(RegistryKey key, string name) => key.GetValue(name) is long value ? value : 0;

    /// <summary>
    /// Opens a subkey for reading, or <see langword="null"/> when it is absent or this process
    /// may not read it (an enumeration skips such a key rather than failing).
    /// </summary>
    public static RegistryKey? TryOpenSubKey(RegistryKey parent, string name)
    {
        try
        {
            return parent.OpenSubKey(name, writable: false);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The subkey names under an open key.</summary>
    public static IReadOnlyList<string> SubKeys(RegistryKey key) => key.GetSubKeyNames();
}
