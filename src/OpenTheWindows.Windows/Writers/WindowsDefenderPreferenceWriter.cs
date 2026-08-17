using System.Globalization;
using System.Management;
using System.Runtime.Versioning;
using System.Text.Json;
using OpenTheWindows.Core.Abstractions;

namespace OpenTheWindows.Windows.Writers;

/// <summary>
/// Sets a Microsoft Defender preference by invoking the <c>Set</c> method of the
/// WMI class <c>MSFT_MpPreference</c> (the same channel as
/// <c>Set-MpPreference</c>). Requires an elevated token.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDefenderPreferenceWriter : IDefenderPreferenceWriter
{
    private const string DefenderNamespace = @"\\.\root\Microsoft\Windows\Defender";
    private const string ReturnValue = "ReturnValue";

    /// <inheritdoc />
    public void SetPreference(string preference, JsonElement value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preference);

        var scope = new ManagementScope(DefenderNamespace);
        scope.Connect();

        using var mpPreference = new ManagementClass(scope, new ManagementPath("MSFT_MpPreference"), null);
        using ManagementBaseObject arguments = mpPreference.GetMethodParameters("Set");
        arguments[preference] = ToCimValue(value);

        using ManagementBaseObject result = mpPreference.InvokeMethod("Set", arguments, null);
        if (result?[ReturnValue] is uint code && code != 0)
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                $"Setting Defender preference '{preference}' failed (return {code})."));
        }
    }

    private static object ToCimValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Number => ToNumber(value),
        JsonValueKind.Array => ToArray(value),
        _ => value.GetRawText(),
    };

    private static object ToNumber(JsonElement value)
    {
        if (value.TryGetInt32(out int i))
        {
            return i;
        }

        return value.TryGetInt64(out long l) ? l : value.GetDouble();
    }

    private static object[] ToArray(JsonElement array)
    {
        object[] items = new object[array.GetArrayLength()];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = ToCimValue(array[i]);
        }

        return items;
    }
}
