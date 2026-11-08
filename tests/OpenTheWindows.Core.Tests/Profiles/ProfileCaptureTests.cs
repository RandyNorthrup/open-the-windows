using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Engine;
using OpenTheWindows.Core.Model;
using OpenTheWindows.Core.Profiles;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.Core.Tests.Profiles;

/// <summary>Capturing a machine as a profile: which entries are scanned, what is kept, and the default id and name.</summary>
public sealed class ProfileCaptureTests
{
    private static readonly TweakCatalog Catalog = CatalogLoader.LoadBuiltIn().Catalog!;
    private static readonly OperatingSystemFacts Facts = FakeOperatingSystemInfo.Windows11Pro24H2();
    private static readonly DateTimeOffset At = new(2026, 11, 7, 23, 30, 0, TimeSpan.FromHours(-5));

    [Fact]
    public void Candidates_are_applicable_non_deprecated_and_verified_unless_drafts_are_asked_for()
    {
        IReadOnlyList<TweakDefinition> verified = ProfileCapture.Candidates(Catalog, Facts, includeDraft: false);
        IReadOnlyList<TweakDefinition> withDrafts = ProfileCapture.Candidates(Catalog, Facts, includeDraft: true);

        Assert.All(verified, e => Assert.Equal(TweakStatus.Verified, e.Status));
        Assert.All(withDrafts, e =>
        {
            Assert.NotEqual(TweakStatus.Deprecated, e.Status);
            Assert.True(e.AppliesTo.Matches(Facts));
        });
        Assert.True(withDrafts.Count >= verified.Count);
        Assert.Contains(withDrafts, e => e.Status == TweakStatus.Draft);
    }

    [Fact]
    public void Candidates_reject_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => ProfileCapture.Candidates(null!, Facts, includeDraft: false));
        Assert.Throws<ArgumentNullException>(() => ProfileCapture.Candidates(Catalog, null!, includeDraft: false));
    }

    [Fact]
    public void The_default_id_uses_the_utc_date_and_the_name_the_computer()
    {
        // 23:30 at UTC-5 is already the next day in UTC.
        Assert.Equal("captured-20261108", ProfileCapture.DefaultId(At));
        Assert.Equal("Captured from LAB-PC", ProfileCapture.DefaultName("LAB-PC"));
    }

    [Fact]
    public void Only_compliant_non_breaking_entries_are_captured()
    {
        TweakDefinition kept = Catalog.Entries.First(e => e.Risk == RiskTier.Safe);
        TweakDefinition drift = Catalog.Entries.First(e => e.Risk == RiskTier.Safe && e.Id != kept.Id);
        TweakDefinition breaking = Catalog.Entries.First(e => e.Risk == RiskTier.Breaking);
        ScanReport report = new(At, Facts, "capture",
        [
            new TweakObservation(kept.Id, ComplianceState.Compliant, kept.Risk, []),
            new TweakObservation(drift.Id, ComplianceState.Drift, drift.Risk, []),
            new TweakObservation(breaking.Id, ComplianceState.Compliant, breaking.Risk, []),
        ]);

        Profile profile = ProfileCapture.FromScan(report, Catalog, ProfileCapture.DefaultId(At), ProfileCapture.DefaultName("LAB-PC"));

        Assert.Equal([kept.Id], profile.Include);
        Assert.Equal(Scope.User, profile.Scope);
        Assert.All(profile.Levels.Values, level => Assert.Equal(Level.Off, level));
    }
}
