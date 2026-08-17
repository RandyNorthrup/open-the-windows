using OpenTheWindows.Core.Updates;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Core.Tests.Updates;

/// <summary>
/// Tests the pause-value computation with a fixed clock: the ISO-8601 UTC format,
/// the 35-day default cap, the extended ceiling, and the six-value mapping
/// (research 03 §5.1).
/// </summary>
public sealed class PauseCalculatorTests
{
    private static readonly DateTimeOffset At = new(2026, 8, 17, 0, 0, 0, TimeSpan.Zero);

    private static PauseCalculator Calc(int ceiling = PauseCalculator.DefaultExtendedCeilingDays)
        => new(new FixedTimeProvider(At), ceiling);

    private static string Value(PausePlan plan, string name)
        => plan.Values.Single(v => string.Equals(v.Name, name, StringComparison.Ordinal)).Value;

    [Fact]
    public void Writes_six_iso_utc_values_for_the_window()
    {
        PausePlan plan = Calc().Calculate(7);

        Assert.Equal(7, plan.Days);
        Assert.False(plan.Extended);
        Assert.Equal(6, plan.Values.Count);
        Assert.Equal("2026-08-17T00:00:00Z", Value(plan, WindowsUpdateSettings.PauseUpdatesStartTime));
        Assert.Equal("2026-08-24T00:00:00Z", Value(plan, WindowsUpdateSettings.PauseUpdatesExpiryTime));
        Assert.Equal("2026-08-17T00:00:00Z", Value(plan, WindowsUpdateSettings.PauseFeatureUpdatesStartTime));
        Assert.Equal("2026-08-24T00:00:00Z", Value(plan, WindowsUpdateSettings.PauseFeatureUpdatesEndTime));
        Assert.Equal("2026-08-17T00:00:00Z", Value(plan, WindowsUpdateSettings.PauseQualityUpdatesStartTime));
        Assert.Equal("2026-08-24T00:00:00Z", Value(plan, WindowsUpdateSettings.PauseQualityUpdatesEndTime));
    }

    [Fact]
    public void Allows_the_35_day_cap()
    {
        PausePlan plan = Calc().Calculate(35);

        Assert.Equal(35, plan.Days);
        Assert.False(plan.Extended);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(36)]
    [InlineData(-1)]
    public void Rejects_out_of_range_without_extended(int days)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Calc().Calculate(days));

    [Fact]
    public void Extended_allows_beyond_the_cap_and_is_flagged()
    {
        PausePlan plan = Calc().Calculate(90, extended: true);

        Assert.Equal(90, plan.Days);
        Assert.True(plan.Extended);
    }

    [Fact]
    public void Thirty_five_days_is_not_flagged_extended_even_with_the_flag()
    {
        PausePlan plan = Calc().Calculate(35, extended: true);

        Assert.False(plan.Extended);
    }

    [Fact]
    public void Extended_rejects_beyond_the_ceiling()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Calc(ceiling: 90).Calculate(91, extended: true));

    [Fact]
    public void A_ceiling_below_the_default_cap_is_rejected()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new PauseCalculator(new FixedTimeProvider(At), 10));
}
