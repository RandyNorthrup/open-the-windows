using System.Text.Json;
using OpenTheWindows.Core.Catalog;

namespace OpenTheWindows.Core.Abstractions;

/// <summary>
/// Writes and deletes registry values. Policy-kind values
/// (<see cref="RegistryValueKind.Policy"/>) are written through the Local Group
/// Policy object (Registry.pol) and mirrored into the live hive so they survive
/// <c>gpupdate</c>; preference-kind values are written directly. User-hive
/// writes target <c>HKEY_USERS\&lt;sid&gt;</c>, never <c>HKEY_CURRENT_USER</c>.
/// </summary>
public interface IRegistryWriter
{
    /// <summary>Writes one value with the given <paramref name="type"/> and JSON <paramref name="data"/>.</summary>
    void Write(RegistryHive hive, string? userSid, string path, string name, RegistryValueType type, JsonElement data, RegistryValueKind kind);

    /// <summary>Deletes one value; used by revert when the prior state was absent.</summary>
    void Delete(RegistryHive hive, string? userSid, string path, string name, RegistryValueKind kind);
}
