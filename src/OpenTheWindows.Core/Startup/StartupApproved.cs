using System.Buffers.Binary;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Startup;

/// <summary>
/// The startup switch Task Manager and Settings › Apps › Startup use. Each Run key and
/// Startup folder has a matching key under <c>Explorer\StartupApproved</c> holding a
/// 12-byte <c>REG_BINARY</c> per app: an odd first byte (<c>03</c>) means turned off, with
/// the time it was turned off as a FILETIME in bytes 4–11; an even one (<c>02</c>, or the
/// <c>04</c> Windows itself writes for some apps), or no value at all, means on. Turning an app off here leaves its Run value or shortcut in
/// place, so it can be turned back on.
/// </summary>
public static class StartupApproved
{
    /// <summary>The <c>StartupApproved</c> key, relative to HKLM or the user's hive.</summary>
    public const string RootPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    private const byte EnabledMarker = 0x02;
    private const byte DisabledMarker = 0x03;
    private const byte DisabledBit = 0x01;
    private const int TimestampOffset = 4;
    private const int ValueLength = 12;

    /// <summary>The hive and key that hold the switch for apps registered at <paramref name="location"/>.</summary>
    public static (RegistryHive Hive, string Path) KeyFor(StartupLocation location) => location switch
    {
        StartupLocation.MachineRun => (RegistryHive.LocalMachine, RootPath + @"\Run"),
        StartupLocation.MachineRun32 => (RegistryHive.LocalMachine, RootPath + @"\Run32"),
        StartupLocation.UserRun => (RegistryHive.User, RootPath + @"\Run"),
        StartupLocation.MachineStartupFolder => (RegistryHive.LocalMachine, RootPath + @"\StartupFolder"),
        StartupLocation.UserStartupFolder => (RegistryHive.User, RootPath + @"\StartupFolder"),
        _ => throw new ArgumentOutOfRangeException(nameof(location), location, "Unknown startup location."),
    };

    /// <summary>Whether a switch value lets the app start: no value, or an even first byte, means on.</summary>
    public static bool IsEnabled(RegistryValueSnapshot value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value is not { Exists: true, Type: RegistryValueType.Binary, Data: { } data })
        {
            return true;
        }

        byte[] bytes = data.GetBytesFromBase64();
        return bytes.Length == 0 || (bytes[0] & DisabledBit) == 0;
    }

    /// <summary>The 12-byte value that turns an app on, or off as of <paramref name="at"/>.</summary>
    public static byte[] Value(bool enabled, DateTimeOffset at)
    {
        byte[] value = new byte[ValueLength];
        value[0] = enabled ? EnabledMarker : DisabledMarker;
        if (!enabled)
        {
            BinaryPrimitives.WriteInt64LittleEndian(value.AsSpan(TimestampOffset), at.ToFileTime());
        }

        return value;
    }
}
