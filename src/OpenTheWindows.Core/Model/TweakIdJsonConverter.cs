using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenTheWindows.Core.Model;

/// <summary>Serialises <see cref="TweakId"/> as its string value and validates on read.</summary>
public sealed class TweakIdJsonConverter : JsonConverter<TweakId>
{
    public override TweakId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? text = reader.GetString();
        if (!TweakId.IsValid(text))
        {
            throw new JsonException($"'{text}' is not a valid tweak id (lower-case kebab-case segments separated by dots).");
        }

        return new TweakId(text!);
    }

    public override void Write(Utf8JsonWriter writer, TweakId value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Value);
    }
}
