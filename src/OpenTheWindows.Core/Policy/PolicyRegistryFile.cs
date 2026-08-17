using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace OpenTheWindows.Core.Policy;

/// <summary>
/// Read-only parser for the Group Policy registry file format (PReg, stored as
/// <c>Registry.pol</c>). The format is documented in <em>Registry Policy File
/// Format</em>: an 8-byte header (<c>PReg</c> signature + version 1) followed by
/// repeated <c>[key;value;type;size;data]</c> records where the brackets and
/// semicolons are literal UTF-16LE characters, the key and value are
/// NUL-terminated UTF-16LE strings, and type/size are little-endian DWORDs.
/// </summary>
/// <remarks>
/// The M2 managed-setting detector uses this to decide whether a Local Group
/// Policy owns a given registry value. The full write path (merge/serialise into
/// the Local GPO) is a later milestone; this reader is deliberately a pure,
/// side-effect-free function over a byte buffer so it is testable with golden
/// fixtures and reusable by that later work.
/// </remarks>
public static class PolicyRegistryFile
{
    // "PReg" as a little-endian DWORD (0x50 0x52 0x65 0x67), then version 1.
    private const uint Signature = 0x67655250;
    private const uint SupportedVersion = 1;

    /// <summary>Parses <paramref name="content"/> into its value entries.</summary>
    /// <exception cref="FormatException">The buffer is not a well-formed PReg file.</exception>
    public static IReadOnlyList<PolicyRegistryEntry> Parse(ReadOnlyMemory<byte> content)
    {
        ReadOnlySpan<byte> span = content.Span;
        if (span.Length < 8
            || BinaryPrimitives.ReadUInt32LittleEndian(span) != Signature
            || BinaryPrimitives.ReadUInt32LittleEndian(span[4..]) != SupportedVersion)
        {
            throw new FormatException("Not a valid Registry.pol (PReg) file: bad signature or version.");
        }

        var entries = new List<PolicyRegistryEntry>();
        int offset = 8;
        while (offset < span.Length)
        {
            Expect(span, ref offset, '[');
            string key = ReadString(span, ref offset);
            Expect(span, ref offset, ';');
            string valueName = ReadString(span, ref offset);
            Expect(span, ref offset, ';');
            uint type = ReadDword(span, ref offset);
            Expect(span, ref offset, ';');
            uint size = ReadDword(span, ref offset);
            Expect(span, ref offset, ';');

            if (size > (uint)(span.Length - offset))
            {
                throw new FormatException("Not a valid Registry.pol (PReg) file: data length exceeds the buffer.");
            }

            ReadOnlyMemory<byte> data = content.Slice(offset, (int)size);
            offset += (int)size;
            Expect(span, ref offset, ']');

            entries.Add(new PolicyRegistryEntry(key, valueName, type, data));
        }

        return entries;
    }

    private static void Expect(ReadOnlySpan<byte> span, ref int offset, char expected)
    {
        if (offset + 2 > span.Length || (char)BinaryPrimitives.ReadUInt16LittleEndian(span[offset..]) != expected)
        {
            throw new FormatException(string.Create(
                CultureInfo.InvariantCulture, $"Not a valid Registry.pol (PReg) file: expected '{expected}'."));
        }

        offset += 2;
    }

    private static string ReadString(ReadOnlySpan<byte> span, ref int offset)
    {
        int start = offset;
        while (offset + 2 <= span.Length)
        {
            ushort unit = BinaryPrimitives.ReadUInt16LittleEndian(span[offset..]);
            offset += 2;
            if (unit == 0)
            {
                return Encoding.Unicode.GetString(span[start..(offset - 2)]);
            }
        }

        throw new FormatException("Not a valid Registry.pol (PReg) file: unterminated string.");
    }

    private static uint ReadDword(ReadOnlySpan<byte> span, ref int offset)
    {
        if (offset + 4 > span.Length)
        {
            throw new FormatException("Not a valid Registry.pol (PReg) file: truncated DWORD.");
        }

        uint value = BinaryPrimitives.ReadUInt32LittleEndian(span[offset..]);
        offset += 4;
        return value;
    }
}
