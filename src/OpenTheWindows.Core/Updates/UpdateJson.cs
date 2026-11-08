using System.Buffers;
using System.Text.Json;

namespace OpenTheWindows.Core.Updates;

/// <summary>JSON values for the registry actions the update guardrails build at run time (no reflection-based serialisation).</summary>
internal static class UpdateJson
{
    /// <summary>A JSON <c>null</c> (the "no Windows default" marker).</summary>
    public static JsonElement Null { get; } = Parse("null");

    /// <summary>A JSON string.</summary>
    public static JsonElement String(string value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStringValue(value);
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    /// <summary>A JSON integer.</summary>
    public static JsonElement Number(int value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteNumberValue(value);
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
