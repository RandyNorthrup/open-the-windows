using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Win32;
using OpenTheWindows.Core.Abstractions;
using CatalogHive = OpenTheWindows.Core.Catalog.RegistryHive;
using CatalogValueType = OpenTheWindows.Core.Catalog.RegistryValueType;

namespace OpenTheWindows.Windows.Readers;

/// <summary>
/// Reads registry values from the 64-bit view. For the user hive it opens
/// <c>HKEY_USERS\&lt;sid&gt;</c> — never <c>HKEY_CURRENT_USER</c> (which is the
/// elevating account). Read-only: only <see cref="RegistryKey.OpenSubKey(string, bool)"/>
/// with <c>writable: false</c> is used. Values are materialised as JSON with a
/// <see cref="Utf8JsonWriter"/> (trim-safe; no reflection-based serialisation).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsRegistryReader : IRegistryReader
{
    private static readonly RegistryValueSnapshot NotFound = new(false, null, null);

    /// <inheritdoc />
    public RegistryValueSnapshot Read(CatalogHive hive, string? userSid, string path, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(name);

        using var key = OpenKey(hive, userSid, path);
        if (key is null)
        {
            return NotFound;
        }

        object? raw = key.GetValue(name, defaultValue: null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return raw is null ? NotFound : Snapshot(key.GetValueKind(name), raw);
    }

    private static RegistryKey? OpenKey(CatalogHive hive, string? userSid, string path)
    {
        if (hive == CatalogHive.LocalMachine)
        {
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            return hklm.OpenSubKey(path, writable: false);
        }

        if (string.IsNullOrEmpty(userSid))
        {
            return null;
        }

        using var users = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64);
        return users.OpenSubKey(string.Create(CultureInfo.InvariantCulture, $@"{userSid}\{path}"), writable: false);
    }

    private static RegistryValueSnapshot Snapshot(RegistryValueKind kind, object raw) => kind switch
    {
        RegistryValueKind.DWord => new RegistryValueSnapshot(true, CatalogValueType.Dword,
            JsonMaterialization.Write(w => w.WriteNumberValue(unchecked((uint)(int)raw)))),
        RegistryValueKind.QWord => new RegistryValueSnapshot(true, CatalogValueType.Qword,
            JsonMaterialization.Write(w => w.WriteNumberValue(unchecked((ulong)(long)raw)))),
        RegistryValueKind.String => new RegistryValueSnapshot(true, CatalogValueType.Sz,
            JsonMaterialization.Write(w => w.WriteStringValue((string)raw))),
        RegistryValueKind.ExpandString => new RegistryValueSnapshot(true, CatalogValueType.ExpandSz,
            JsonMaterialization.Write(w => w.WriteStringValue((string)raw))),
        RegistryValueKind.MultiString => new RegistryValueSnapshot(true, CatalogValueType.MultiSz, WriteStrings((string[])raw)),
        RegistryValueKind.Binary => new RegistryValueSnapshot(true, CatalogValueType.Binary,
            JsonMaterialization.Write(w => w.WriteStringValue(Convert.ToBase64String((byte[])raw)))),
        _ => new RegistryValueSnapshot(true, null, null),
    };

    private static JsonElement WriteStrings(string[] values) => JsonMaterialization.Write(writer =>
    {
        writer.WriteStartArray();
        foreach (string value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    });
}
