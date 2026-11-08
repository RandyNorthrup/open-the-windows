using OpenTheWindows.App.Resources;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Journal;

namespace OpenTheWindows.App.Tests.ViewModels;

/// <summary>Every apply, integrity and scan state has its own human-readable label (none falls back to the enum name).</summary>
public sealed class DisplayLabelsTests
{
    [Fact]
    public void Every_apply_state_has_a_distinct_label()
    {
        ApplyState[] states = Enum.GetValues<ApplyState>();

        Assert.Equal(states.Length, states.Select(DisplayLabels.For).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("Not reversible", DisplayLabels.For(ApplyState.NotReversible));
    }

    [Fact]
    public void Every_journal_integrity_has_a_distinct_label()
    {
        JournalIntegrity[] values = Enum.GetValues<JournalIntegrity>();

        Assert.Equal(values.Length, values.Select(DisplayLabels.For).Distinct(StringComparer.Ordinal).Count());
        Assert.All(values.Where(v => v != JournalIntegrity.Verified), v => Assert.NotEqual(v.ToString(), DisplayLabels.For(v), StringComparer.Ordinal));
    }

    [Fact]
    public void Every_compliance_state_has_a_distinct_label()
    {
        ComplianceState[] values = Enum.GetValues<ComplianceState>();

        Assert.Equal(values.Length, values.Select(DisplayLabels.For).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("In place", DisplayLabels.For(ComplianceState.Compliant));
        Assert.Equal("Not applied", DisplayLabels.For(ComplianceState.Drift));
    }
}
