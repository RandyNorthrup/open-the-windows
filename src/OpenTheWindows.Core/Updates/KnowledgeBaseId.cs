using System.Globalization;

namespace OpenTheWindows.Core.Updates;

/// <summary>Normalises a knowledge-base article id (<c>KB5063878</c> or <c>5063878</c>) to digits.</summary>
public static class KnowledgeBaseId
{
    /// <summary>Prefix users often type in front of a KB number.</summary>
    public const string Prefix = "KB";

    /// <summary>
    /// Returns the digit string, or <see langword="null"/> when <paramref name="value"/>
    /// is empty or not a KB number.
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[Prefix.Length..];
        }

        if (trimmed.Length == 0 || !trimmed.All(char.IsAsciiDigit))
        {
            return null;
        }

        return trimmed;
    }

    /// <summary>Formats a normalised id as <c>KBnnnn</c> for display.</summary>
    public static string Format(string normalized)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalized);
        return string.Create(CultureInfo.InvariantCulture, $"{Prefix}{normalized}");
    }
}
