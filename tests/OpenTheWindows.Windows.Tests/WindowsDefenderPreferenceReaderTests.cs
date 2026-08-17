using System.Text.Json;
using OpenTheWindows.Windows.Readers;

namespace OpenTheWindows.Windows.Tests;

/// <summary>
/// Reads real Microsoft Defender preferences through WMI (read-only, no
/// elevation needed). Uses preferences present on every Defender-enabled box.
/// </summary>
public sealed class WindowsDefenderPreferenceReaderTests
{
    [Fact]
    public void Reads_a_numeric_preference()
    {
        var reader = new WindowsDefenderPreferenceReader();

        // PUAProtection is an enum stored as a small integer.
        JsonElement? value = reader.Read("PUAProtection");

        Assert.NotNull(value);
        Assert.Equal(JsonValueKind.Number, value.Value.ValueKind);
    }

    [Fact]
    public void Reads_a_boolean_preference()
    {
        var reader = new WindowsDefenderPreferenceReader();

        JsonElement? value = reader.Read("DisableRealtimeMonitoring");

        Assert.NotNull(value);
        Assert.True(value.Value.ValueKind is JsonValueKind.True or JsonValueKind.False);
    }

    [Fact]
    public void Unknown_preference_is_null()
    {
        var reader = new WindowsDefenderPreferenceReader();

        JsonElement? value = reader.Read("OtwNoSuchDefenderPreference");

        Assert.Null(value);
    }
}
