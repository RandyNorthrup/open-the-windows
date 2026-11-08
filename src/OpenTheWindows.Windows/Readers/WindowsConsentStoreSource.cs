using System.Runtime.Versioning;
using Microsoft.Win32;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Privacy;

namespace OpenTheWindows.Windows.Readers;

/// <summary>
/// <see cref="IConsentStoreSource"/> over <c>CapabilityAccessManager\ConsentStore</c> in HKLM
/// and the signed-in user's hive: one record per app key under each capability, with Store
/// apps directly under the capability and desktop apps under its <c>NonPackaged</c> key.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsConsentStoreSource : IConsentStoreSource
{
    /// <summary>The ConsentStore key, relative to HKLM or a user's hive.</summary>
    public const string ConsentStorePath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";

    private const string NonPackaged = "NonPackaged";
    private const string LastUsedTimeStart = "LastUsedTimeStart";
    private const string LastUsedTimeStop = "LastUsedTimeStop";

    /// <inheritdoc />
    public IReadOnlyList<ConsentStoreRecord> List(string? userSid)
    {
        List<ConsentStoreRecord> records = [.. Read(null)];
        if (!string.IsNullOrEmpty(userSid))
        {
            records.AddRange(Read(userSid));
        }

        return records;
    }

    private static List<ConsentStoreRecord> Read(string? userSid)
    {
        List<ConsentStoreRecord> records = [];
        using RegistryKey? store = RegistryEnumeration.Open(userSid, ConsentStorePath);
        if (store is null)
        {
            return records;
        }

        foreach (string capability in RegistryEnumeration.SubKeys(store))
        {
            using RegistryKey? capabilityKey = RegistryEnumeration.TryOpenSubKey(store, capability);
            if (capabilityKey is null)
            {
                continue;
            }

            records.AddRange(Apps(capabilityKey, capability, desktop: false, perUser: userSid is not null));
            using RegistryKey? desktopKey = RegistryEnumeration.TryOpenSubKey(capabilityKey, NonPackaged);
            if (desktopKey is not null)
            {
                records.AddRange(Apps(desktopKey, capability, desktop: true, perUser: userSid is not null));
            }
        }

        return records;
    }

    private static List<ConsentStoreRecord> Apps(RegistryKey parent, string capability, bool desktop, bool perUser)
    {
        List<ConsentStoreRecord> apps = [];
        foreach (string name in RegistryEnumeration.SubKeys(parent).Where(n => !string.Equals(n, NonPackaged, StringComparison.OrdinalIgnoreCase)))
        {
            using RegistryKey? app = RegistryEnumeration.TryOpenSubKey(parent, name);
            if (app is not null)
            {
                apps.Add(new ConsentStoreRecord(
                    capability, name, desktop, perUser, RegistryEnumeration.QWord(app, LastUsedTimeStart), RegistryEnumeration.QWord(app, LastUsedTimeStop)));
            }
        }

        return apps;
    }
}
