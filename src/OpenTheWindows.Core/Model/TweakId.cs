namespace OpenTheWindows.Core.Model;

/// <summary>
/// Stable identifier of a catalogue entry: lower-case kebab-case segments
/// separated by dots (e.g. <c>privacy.diagnostic-data.allow-telemetry</c>).
/// Validated on construction so an invalid id can never reach the engine.
/// </summary>
/// <remarks>
/// Grammar: <c>segment ("." segment)*</c> where
/// <c>segment = word ("-" word)*</c> and <c>word = [a-z0-9]+</c>.
/// Validation is a single linear pass; no regular expression, so there is no
/// backtracking to reason about and the check is safe on untrusted input.
/// </remarks>
public readonly record struct TweakId
{
    /// <summary>Maximum accepted length; keeps ids readable in logs and Event Log entries.</summary>
    public const int MaxLength = 96;

    private const char SegmentSeparator = '.';
    private const char WordSeparator = '-';

    public TweakId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length > MaxLength)
        {
            throw new ArgumentException(
                $"Tweak id '{value}' is longer than {MaxLength} characters.",
                nameof(value));
        }

        if (!IsWellFormed(value))
        {
            throw new ArgumentException(
                $"Tweak id '{value}' must be lower-case kebab-case segments separated by dots " +
                "(letters a-z, digits, '-' inside a segment, '.' between segments).",
                nameof(value));
        }

        Value = value;
    }

    /// <summary>The validated identifier text.</summary>
    public string Value { get; }

    /// <summary>Returns <see langword="true"/> when <paramref name="value"/> is a well-formed id.</summary>
    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= MaxLength && IsWellFormed(value);

    public override string ToString() => Value;

    private static bool IsWellFormed(string value)
    {
        // Every '.' and '-' must be surrounded by word characters; the string
        // must start and end with a word character.
        char previous = SegmentSeparator; // sentinel: start behaves like "after a separator"
        foreach (char c in value)
        {
            bool isWordChar = c is (>= 'a' and <= 'z') or (>= '0' and <= '9');
            bool isSeparator = c is SegmentSeparator or WordSeparator;

            if (!isWordChar && !isSeparator)
            {
                return false;
            }

            if (isSeparator && (previous is SegmentSeparator or WordSeparator))
            {
                return false; // empty word: leading separator, "--", ".-", "-.", ".."
            }

            previous = c;
        }

        return previous is not (SegmentSeparator or WordSeparator); // no trailing separator
    }
}
