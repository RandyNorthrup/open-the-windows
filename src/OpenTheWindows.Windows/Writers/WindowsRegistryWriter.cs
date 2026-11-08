using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Win32;
using OpenTheWindows.Core.Abstractions;
using CatalogHive = OpenTheWindows.Core.Catalog.RegistryHive;
using CatalogValueKind = OpenTheWindows.Core.Catalog.RegistryValueKind;
using CatalogValueType = OpenTheWindows.Core.Catalog.RegistryValueType;
using Win32Hive = Microsoft.Win32.RegistryHive;
using Win32ValueKind = Microsoft.Win32.RegistryValueKind;

namespace OpenTheWindows.Windows.Writers;

/// <summary>
/// Writes and deletes registry values in the 64-bit view. Machine-scope policy
/// values are written through the Local GPO (so they survive <c>gpupdate</c>)
/// and mirrored into the live hive for immediate effect — or, on Windows Home, which
/// has no Group Policy engine, straight to the live policy key; preference values and
/// user-scope values are written directly. User-hive writes target
/// <c>HKEY_USERS\&lt;sid&gt;</c>, never <c>HKEY_CURRENT_USER</c>.
/// </summary>
/// <remarks>
/// Per-user policy values are written directly to the user's <c>Software\Policies</c>
/// (mirror only); per-user Local GPO (MLGPO) authoring is deferred to M5. Machine
/// policy — the acceptance-critical path — goes through <see cref="LocalGroupPolicy"/>.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsRegistryWriter : IRegistryWriter
{
    private static readonly TimeSpan GroupPolicyWait = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan GroupPolicyPoll = TimeSpan.FromMilliseconds(500);

    /// <inheritdoc />
    public void Write(CatalogHive hive, string? userSid, string path, string name, CatalogValueType type, JsonElement data, CatalogValueKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(name);

        (object value, Win32ValueKind valueKind) = ToRegistryValue(type, data);
        if (kind == CatalogValueKind.Policy && hive == CatalogHive.LocalMachine && LocalGroupPolicy.IsAvailable)
        {
            LocalGroupPolicy.Write(path, name, value, valueKind);
            try
            {
                WriteLive(hive, userSid, path, name, value, valueKind);
            }
            catch (UnauthorizedAccessException) when (ApplyThroughGroupPolicy(path, name, live => Same(live, value)))
            {
                // A protected value only Group Policy itself may write (see ApplyThroughGroupPolicy).
            }

            return;
        }

        WriteLive(hive, userSid, path, name, value, valueKind);
    }

    /// <inheritdoc />
    public void Delete(CatalogHive hive, string? userSid, string path, string name, CatalogValueKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(name);

        if (kind == CatalogValueKind.Policy && hive == CatalogHive.LocalMachine && LocalGroupPolicy.IsAvailable)
        {
            LocalGroupPolicy.Delete(path, name);
            try
            {
                DeleteLive(hive, userSid, path, name);
            }
            catch (UnauthorizedAccessException) when (ApplyThroughGroupPolicy(path, name, live => live is null))
            {
                // Group Policy removes a value it applied once the entry is gone from the store.
            }

            return;
        }

        DeleteLive(hive, userSid, path, name);
    }

    private static void DeleteLive(CatalogHive hive, string? userSid, string path, string name)
    {
        using RegistryKey? key = OpenWritable(hive, userSid, path, create: false);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }

    /// <summary>
    /// Some machine policy values (the Widgets policy among them, guarded by Windows' User Choice
    /// Protection driver) refuse every writer except the Group Policy service itself. The Local GPO
    /// store already holds the change, so ask Group Policy to apply it now and wait until the live
    /// value reads as wanted. Returns <see langword="false"/> (so the original refusal stands) when it
    /// does not within <see cref="GroupPolicyWait"/>.
    /// </summary>
    private static bool ApplyThroughGroupPolicy(string path, string name, Func<object?, bool> wanted)
    {
        Interop.GroupPolicyRefresh.Machine();
        int checks = (int)(GroupPolicyWait / GroupPolicyPoll);
        for (int check = 0; check < checks; check++)
        {
            using var hklm = RegistryKey.OpenBaseKey(Win32Hive.LocalMachine, RegistryView.Registry64);
            using RegistryKey? key = hklm.OpenSubKey(path, writable: false);
            if (wanted(key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)))
            {
                return true;
            }

            Thread.Sleep(GroupPolicyPoll);
        }

        return false;
    }

    private static bool Same(object? live, object wanted) => (live, wanted) switch
    {
        (byte[] a, byte[] b) => a.AsSpan().SequenceEqual(b),
        (string[] a, string[] b) => a.SequenceEqual(b, StringComparer.Ordinal),
        _ => Equals(live, wanted),
    };

    private static void WriteLive(CatalogHive hive, string? userSid, string path, string name, object value, Win32ValueKind valueKind)
    {
        using RegistryKey key = OpenWritable(hive, userSid, path, create: true)
            ?? throw new InvalidOperationException(
                string.Create(CultureInfo.InvariantCulture, $"Could not open registry key '{path}' for writing."));
        key.SetValue(name, value, valueKind);
    }

    private static RegistryKey? OpenWritable(CatalogHive hive, string? userSid, string path, bool create)
    {
        if (hive == CatalogHive.LocalMachine)
        {
            using var hklm = RegistryKey.OpenBaseKey(Win32Hive.LocalMachine, RegistryView.Registry64);
            return create ? hklm.CreateSubKey(path, writable: true) : hklm.OpenSubKey(path, writable: true);
        }

        if (string.IsNullOrEmpty(userSid))
        {
            throw new InvalidOperationException("A user SID is required to write the per-user hive.");
        }

        using var users = RegistryKey.OpenBaseKey(Win32Hive.Users, RegistryView.Registry64);
        using (RegistryKey? userRoot = users.OpenSubKey(userSid, writable: false))
        {
            if (userRoot is null)
            {
                throw new InvalidOperationException(
                    string.Create(CultureInfo.InvariantCulture, $"User hive HKU\\{userSid} is not loaded (UserHiveNotLoaded)."));
            }
        }

        string subPath = string.Create(CultureInfo.InvariantCulture, $@"{userSid}\{path}");
        return create ? users.CreateSubKey(subPath, writable: true) : users.OpenSubKey(subPath, writable: true);
    }

    private static (object Value, Win32ValueKind Kind) ToRegistryValue(CatalogValueType type, JsonElement data) => type switch
    {
        CatalogValueType.Dword => (unchecked((int)data.GetUInt32()), Win32ValueKind.DWord),
        CatalogValueType.Qword => (unchecked((long)data.GetUInt64()), Win32ValueKind.QWord),
        CatalogValueType.Sz => (data.GetString() ?? string.Empty, Win32ValueKind.String),
        CatalogValueType.ExpandSz => (data.GetString() ?? string.Empty, Win32ValueKind.ExpandString),
        CatalogValueType.MultiSz => (ToStringArray(data), Win32ValueKind.MultiString),
        CatalogValueType.Binary => (data.GetBytesFromBase64(), Win32ValueKind.Binary),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported registry value type."),
    };

    private static string[] ToStringArray(JsonElement array)
    {
        string[] values = new string[array.GetArrayLength()];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = array[i].GetString() ?? string.Empty;
        }

        return values;
    }
}
