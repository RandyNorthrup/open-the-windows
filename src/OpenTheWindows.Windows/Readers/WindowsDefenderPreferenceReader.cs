using System.Collections;
using System.Globalization;
using System.Management;
using System.Runtime.Versioning;
using System.Text.Json;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows.Readers;

/// <summary>
/// Reads a Microsoft Defender preference from WMI
/// (<c>MSFT_MpPreference</c> in <c>root\Microsoft\Windows\Defender</c>) and
/// returns it as JSON. Read-only: it never calls the class's Set/Add/Remove
/// methods.
/// </summary>
/// <remarks>
/// <c>MSFT_MpPreference</c> is a singleton, so the query returns one instance.
/// A named property is materialised to a <see cref="JsonElement"/> with a
/// <see cref="Utf8JsonWriter"/> (trim-safe). When Defender is absent (the WMI
/// namespace or class is missing) or the property is unknown, the read returns
/// <see langword="null"/>. Some properties (e.g. exclusion lists) are only
/// legible to an administrator; the reader returns whatever value WMI provides
/// without attempting to elevate.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsDefenderPreferenceReader : IDefenderPreferenceReader
{
    private const string DefenderNamespace = @"\\.\root\Microsoft\Windows\Defender";

    /// <inheritdoc />
    public JsonElement? Read(string propertyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        try
        {
            using var searcher = new ManagementObjectSearcher(
                new ManagementScope(DefenderNamespace),
                new ObjectQuery("SELECT * FROM MSFT_MpPreference"));
            using ManagementObjectCollection results = searcher.Get();
            using ManagementBaseObject? instance = results.Cast<ManagementBaseObject>().FirstOrDefault();
            if (instance is null)
            {
                return null;
            }

            PropertyData? property = instance.Properties
                .Cast<PropertyData>()
                .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));
            if (property is null)
            {
                return null;
            }

            return JsonMaterialization.Write(writer => WriteCimValue(writer, property.Value));
        }
        catch (ManagementException)
        {
            // The Defender WMI namespace/class is unavailable (e.g. Defender removed).
            return null;
        }
    }

    private static void WriteCimValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case byte or sbyte or short or ushort or int or uint or long:
                writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                break;
            case ulong number:
                writer.WriteNumberValue(number);
                break;
            case IEnumerable sequence:
                writer.WriteStartArray();
                foreach (object? item in sequence)
                {
                    WriteCimValue(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }
}
