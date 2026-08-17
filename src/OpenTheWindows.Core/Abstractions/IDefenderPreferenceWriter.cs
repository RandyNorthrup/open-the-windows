using System.Text.Json;

namespace OpenTheWindows.Core.Abstractions;

/// <summary>Sets a Microsoft Defender preference (WMI <c>MSFT_MpPreference</c> <c>Set</c> method).</summary>
public interface IDefenderPreferenceWriter
{
    /// <summary>Sets one preference to <paramref name="value"/>.</summary>
    void SetPreference(string preference, JsonElement value);
}
