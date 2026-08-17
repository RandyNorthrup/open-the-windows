using System.Text.Json;

namespace OpenTheWindows.Core.Catalog.Actions;

/// <summary>
/// Sets one registry value. <see cref="Default"/> is the documented Windows
/// default used by "reset to Windows defaults"; a JSON <c>null</c> means the
/// value is absent by default (delete on reset). Revert of an apply always
/// replays the journaled prior state, not this default.
/// </summary>
/// <param name="Hive">Machine or the target user's hive.</param>
/// <param name="Path">Key path below the hive, backslash-separated, no leading slash.</param>
/// <param name="Name">Value name; empty string for the key's default value.</param>
/// <param name="Type">Registry value type; JSON <see cref="Value"/> must match.</param>
/// <param name="Value">Desired data (number, string, string array or base64 per <see cref="Type"/>).</param>
/// <param name="Default">Windows default data, or JSON null when the value does not exist by default.</param>
/// <param name="ValueKind">Policy (written through the Local GPO) or preference.</param>
public sealed record RegistryAction(
    RegistryHive Hive,
    string Path,
    string Name,
    RegistryValueType Type,
    JsonElement Value,
    JsonElement Default,
    RegistryValueKind ValueKind) : ITweakAction
{
    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public string Kind => "Registry";
}
