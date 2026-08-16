using System.Text.Json;

namespace OpenTheWindows.Core.Catalog.Actions;

/// <summary>Sets a Microsoft Defender preference (WMI <c>MSFT_MpPreference</c>, i.e. <c>Set-MpPreference</c>).</summary>
/// <param name="Property">Preference name, e.g. <c>PUAProtection</c>.</param>
/// <param name="Value">Desired value.</param>
/// <param name="Default">Windows default value.</param>
public sealed record DefenderPreferenceAction(string Property, JsonElement Value, JsonElement Default) : ITweakAction
{
    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public string Kind => "DefenderPreference";
}
