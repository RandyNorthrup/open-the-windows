using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Updates;

namespace OpenTheWindows.Core.Tests.Updates;

/// <summary>
/// Tests the embedded end-of-servicing calendar (research 03 §4.2): lookup, the
/// days-until computation, the 90-day warning window, the consumer/enterprise
/// split and unknown-version handling.
/// </summary>
public sealed class EndOfServicingCalendarTests
{
    private static readonly DateTimeOffset At = new(2026, 8, 17, 0, 0, 0, TimeSpan.Zero);

    private static EndOfServicingCalendar Calendar() => EndOfServicingCalendar.LoadBuiltIn();

    [Fact]
    public void Loads_the_built_in_calendar()
        => Assert.NotNull(Calendar().Find("24H2", EndOfServicingEditionFamily.Consumer));

    [Fact]
    public void Computes_days_until_end_of_service()
    {
        // 24H2 consumer end-of-service is 2026-10-13; from 2026-08-17 that is 57 days.
        int? days = Calendar().DaysUntilEndOfService("24H2", EndOfServicingEditionFamily.Consumer, At);

        Assert.Equal(57, days);
    }

    [Fact]
    public void Enterprise_gets_longer_servicing_than_consumer()
    {
        EndOfServicingCalendar calendar = Calendar();

        int consumer = calendar.DaysUntilEndOfService("24H2", EndOfServicingEditionFamily.Consumer, At)!.Value;
        int enterprise = calendar.DaysUntilEndOfService("24H2", EndOfServicingEditionFamily.Enterprise, At)!.Value;

        Assert.True(enterprise > consumer);
    }

    [Fact]
    public void Warns_within_ninety_days()
        => Assert.True(Calendar().IsWithinWarningWindow("24H2", EndOfServicingEditionFamily.Consumer, At));

    [Fact]
    public void Does_not_warn_when_far_from_end_of_service()
        => Assert.False(Calendar().IsWithinWarningWindow("25H2", EndOfServicingEditionFamily.Consumer, At));

    [Fact]
    public void Unknown_version_is_null_and_never_warns()
    {
        EndOfServicingCalendar calendar = Calendar();

        Assert.Null(calendar.DaysUntilEndOfService("19H1", EndOfServicingEditionFamily.Consumer, At));
        Assert.False(calendar.IsWithinWarningWindow("19H1", EndOfServicingEditionFamily.Consumer, At));
    }

    [Theory]
    [InlineData(WindowsEdition.Home, EndOfServicingEditionFamily.Consumer)]
    [InlineData(WindowsEdition.Pro, EndOfServicingEditionFamily.Consumer)]
    [InlineData(WindowsEdition.Enterprise, EndOfServicingEditionFamily.Enterprise)]
    [InlineData(WindowsEdition.Education, EndOfServicingEditionFamily.Enterprise)]
    public void Maps_edition_to_servicing_family(WindowsEdition edition, EndOfServicingEditionFamily expected)
        => Assert.Equal(expected, EndOfServicingCalendar.FamilyFor(edition));
}
