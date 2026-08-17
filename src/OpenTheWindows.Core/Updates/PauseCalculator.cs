using System.Globalization;

namespace OpenTheWindows.Core.Updates;

/// <summary>
/// Computes the six Settings-app pause registry values for an N-day pause
/// (research 03 §5.1). The values are ISO-8601 UTC strings in the exact format
/// the Settings app writes (<c>yyyy-MM-ddTHH:mm:ssZ</c>). Enforces the guardrail
/// caps: a default 35-day maximum (Microsoft's limit), and an opt-in extended
/// pause up to a configurable ceiling (default 365 days) which the update-control
/// service treats as Advanced risk.
/// </summary>
public sealed class PauseCalculator
{
    /// <summary>The shortest pause that makes sense.</summary>
    public const int MinDays = 1;

    /// <summary>Microsoft's documented pause cap (research 03 §5.1).</summary>
    public const int DefaultMaxDays = 35;

    /// <summary>The default ceiling for an extended pause (research 03 §7.2, spec M4).</summary>
    public const int DefaultExtendedCeilingDays = 365;

    private const string IsoUtcFormat = "yyyy-MM-ddTHH:mm:ss'Z'";

    private readonly TimeProvider _time;
    private readonly int _extendedCeilingDays;

    /// <summary>Creates the calculator with the given clock and extended ceiling (defaults to <see cref="DefaultExtendedCeilingDays"/>).</summary>
    public PauseCalculator(TimeProvider time, int extendedCeilingDays = DefaultExtendedCeilingDays)
    {
        _time = time ?? throw new ArgumentNullException(nameof(time));
        if (extendedCeilingDays < DefaultMaxDays)
        {
            throw new ArgumentOutOfRangeException(nameof(extendedCeilingDays),
                extendedCeilingDays, "The extended ceiling cannot be below the default 35-day cap.");
        }

        _extendedCeilingDays = extendedCeilingDays;
    }

    /// <summary>
    /// Computes the pause values for <paramref name="days"/> days. Without
    /// <paramref name="extended"/> the cap is <see cref="DefaultMaxDays"/>; with it,
    /// the cap rises to the configured ceiling. Throws
    /// <see cref="ArgumentOutOfRangeException"/> when the request is out of range.
    /// </summary>
    public PausePlan Calculate(int days, bool extended = false)
    {
        int cap = extended ? _extendedCeilingDays : DefaultMaxDays;
        if (days < MinDays || days > cap)
        {
            throw new ArgumentOutOfRangeException(nameof(days), days,
                string.Create(CultureInfo.InvariantCulture, $"Pause must be between {MinDays} and {cap} days{(extended ? " (extended)" : string.Empty)}."));
        }

        DateTimeOffset start = _time.GetUtcNow();
        DateTimeOffset expiry = start.AddDays(days);
        string startText = Format(start);
        string expiryText = Format(expiry);

        IReadOnlyList<PauseRegistryValue> values =
        [
            new(WindowsUpdateSettings.PauseUpdatesStartTime, startText),
            new(WindowsUpdateSettings.PauseUpdatesExpiryTime, expiryText),
            new(WindowsUpdateSettings.PauseFeatureUpdatesStartTime, startText),
            new(WindowsUpdateSettings.PauseFeatureUpdatesEndTime, expiryText),
            new(WindowsUpdateSettings.PauseQualityUpdatesStartTime, startText),
            new(WindowsUpdateSettings.PauseQualityUpdatesEndTime, expiryText),
        ];

        return new PausePlan(start, expiry, days, extended && days > DefaultMaxDays, values);
    }

    private static string Format(DateTimeOffset instant)
        => instant.UtcDateTime.ToString(IsoUtcFormat, CultureInfo.InvariantCulture);
}
