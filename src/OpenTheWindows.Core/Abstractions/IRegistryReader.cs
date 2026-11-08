using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Reads a single registry value. Never writes. For <see cref="RegistryHive.User"/>
/// the caller supplies the target user's SID so the reader opens
/// <c>HKEY_USERS\&lt;sid&gt;</c> — never the elevating process's <c>HKCU</c>.
/// </summary>
public interface IRegistryReader
{
    /// <summary>Reads the value at <paramref name="path"/>/<paramref name="name"/>.</summary>
    /// <param name="hive">Machine hive, or the user hive (opened via <paramref name="userSid"/>).</param>
    /// <param name="userSid">Target user SID for the user hive; ignored for the machine hive.</param>
    /// <param name="path">Key path below the hive, backslash-separated, no leading slash.</param>
    /// <param name="name">Value name; empty string for the key's default value.</param>
    RegistryValueSnapshot Read(RegistryHive hive, string? userSid, string path, string name);
}
