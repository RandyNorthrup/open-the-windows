using System.Text.Json;

namespace OpenTheWindows.Core.Abstractions;

/// <summary>Reads a Microsoft Defender preference (WMI <c>MSFT_MpPreference</c>). Never writes.</summary>
public interface IDefenderPreferenceReader
{
    /// <summary>
    /// Returns the preference value as JSON, or <see langword="null"/> when the
    /// property is unavailable (Defender absent or property unknown).
    /// </summary>
    JsonElement? Read(string propertyName);
}
