using System.Buffers;
using System.Text.Json;

namespace OpenTheWindows.Windows.Readers;

/// <summary>
/// Materialises a value written through a <see cref="Utf8JsonWriter"/> into a
/// detached <see cref="JsonElement"/>. The OS readers use this instead of
/// reflection-based serialisation (<c>JsonSerializer.SerializeToElement</c>) so
/// the trimmable Windows project stays reflection-free (no IL2026).
/// </summary>
internal static class JsonMaterialization
{
    /// <summary>
    /// Invokes <paramref name="write"/> against a temporary writer and returns
    /// the resulting JSON as a detached (cloned) <see cref="JsonElement"/>.
    /// </summary>
    public static JsonElement Write(Action<Utf8JsonWriter> write)
    {
        ArgumentNullException.ThrowIfNull(write);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}
