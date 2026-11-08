using System.Globalization;

namespace OpenTheWindows.Core.Catalog;

/// <summary>
/// Validates the setting name and value of a security-policy action before they
/// are written into a <c>secedit</c> template. A template is an INI file, so a
/// line break, a section bracket or an equals sign in either part could smuggle in
/// extra settings (group membership, user rights); only a letters-only setting
/// name and a plain integer value are accepted.
/// </summary>
public static class SecurityPolicyValues
{
    private const int MaximumValueLength = 10;

    /// <summary>Returns why the pair is refused, or <see langword="null"/> when it is safe to template.</summary>
    public static string? Refusal(string setting, string value)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(value);

        if (setting.Length == 0 || !setting.All(char.IsAsciiLetter))
        {
            return string.Create(CultureInfo.InvariantCulture, $"Security policy setting '{setting}' must be letters only.");
        }

        string digits = value.StartsWith('-') ? value[1..] : value;
        bool integer = digits.Length is > 0 and <= MaximumValueLength && digits.All(char.IsAsciiDigit);
        return integer
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"Security policy value '{value}' for '{setting}' must be a plain integer.");
    }
}
