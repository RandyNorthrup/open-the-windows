using System.Buffers.Binary;
using System.Text;

namespace OpenTheWindows.TestSupport;

/// <summary>
/// Builds in-memory Group Policy registry buffers (PReg, the
/// <c>Registry.pol</c> format) for tests: an 8-byte header followed by
/// <c>[key;value;type;size;data]</c> records with UTF-16LE strings and
/// little-endian DWORDs.
/// </summary>
public static class PRegFixture
{
    /// <summary>Builds a PReg buffer containing <paramref name="entries"/>.</summary>
    public static byte[] Build(params (string Key, string Value, uint Type, byte[] Data)[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var buffer = new List<byte>();
        buffer.AddRange("PReg"u8);
        AppendDword(buffer, 1u);

        foreach ((string key, string value, uint type, byte[] data) in entries)
        {
            AppendChar(buffer, '[');
            AppendString(buffer, key);
            AppendChar(buffer, ';');
            AppendString(buffer, value);
            AppendChar(buffer, ';');
            AppendDword(buffer, type);
            AppendChar(buffer, ';');
            AppendDword(buffer, (uint)data.Length);
            AppendChar(buffer, ';');
            buffer.AddRange(data);
            AppendChar(buffer, ']');
        }

        return [.. buffer];
    }

    private static void AppendChar(List<byte> buffer, char value)
    {
        buffer.Add((byte)value);
        buffer.Add(0);
    }

    private static void AppendString(List<byte> buffer, string value)
    {
        buffer.AddRange(Encoding.Unicode.GetBytes(value));
        buffer.Add(0);
        buffer.Add(0);
    }

    private static void AppendDword(List<byte> buffer, uint value)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(word, value);
        buffer.AddRange(word);
    }
}
