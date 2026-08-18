using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.Core.Tests.Engine;

public sealed class BuildChangeTests
{
    private static OperatingSystemFacts Facts(int build, int revision)
        => new("Windows 11 Pro", "Professional", "24H2", build, revision, "x64", "Client");

    [Fact]
    public void No_prior_journal_is_treated_as_changed_with_no_baseline()
    {
        BuildChangeStatus status = BuildChange.Detect(Facts(26100, 4351), previous: null);

        Assert.True(status.Changed);
        Assert.Null(status.Previous);
        Assert.Equal("26100.4351", status.Current);
    }

    [Fact]
    public void Same_build_and_ubr_is_unchanged()
    {
        BuildChangeStatus status = BuildChange.Detect(Facts(26100, 4351), new JournalOs(26100, 4351, "Professional"));

        Assert.False(status.Changed);
        Assert.Equal("26100.4351", status.Current);
        Assert.Equal("26100.4351", status.Previous);
    }

    [Fact]
    public void A_new_ubr_is_a_change()
    {
        BuildChangeStatus status = BuildChange.Detect(Facts(26100, 4600), new JournalOs(26100, 4351, "Professional"));

        Assert.True(status.Changed);
        Assert.Equal("26100.4600", status.Current);
        Assert.Equal("26100.4351", status.Previous);
    }

    [Fact]
    public void A_new_major_build_is_a_change()
    {
        BuildChangeStatus status = BuildChange.Detect(Facts(26200, 1000), new JournalOs(26100, 4351, "Professional"));

        Assert.True(status.Changed);
        Assert.Equal("26200.1000", status.Current);
        Assert.Equal("26100.4351", status.Previous);
    }
}
