using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Services;
using OpenTheWindows.App.Tests.Fakes;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core.Abstractions;
using OpenTheWindows.Core.Audit;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.App.Tests.ViewModels;

/// <summary>
/// The shared category page: level dial selection, search, risk/draft filters
/// and the detail selection. Driven against the real built-in catalogue and a
/// fixed machine so the behaviour is exercised end to end (no UI thread).
/// </summary>
public sealed class CategoryPageViewModelTests
{
    private static readonly TweakCatalog Catalog = CatalogLoader.LoadBuiltIn().Catalog!;
    private static readonly OperatingSystemFacts Facts = FakeOperatingSystemInfo.Windows11Pro24H2();
    private static readonly IReadOnlyList<Baseline> Baselines = BaselineLoader.LoadBuiltIn().Baselines;

    private static CategoryPageViewModel Build(Category category = Category.Privacy) => Build(category, new FakeApplyFlowLauncher());

    private static CategoryPageViewModel Build(Category category, IApplyFlowLauncher launcher) => Build(category, launcher, new AppSettings());

    private static CategoryPageViewModel Build(Category category, IApplyFlowLauncher launcher, AppSettings settings) => new(category, Catalog, Facts, Baselines, launcher, settings);

    [Fact]
    public void Is_the_matching_page_with_applicable_entries()
    {
        CategoryPageViewModel page = Build(Category.Privacy);

        Assert.Equal(PageKeys.Privacy, page.Key);
        Assert.NotEmpty(page.AllItems);
        Assert.All(page.AllItems, item => Assert.NotEqual(TweakStatus.Deprecated, item.Status));
    }

    [Fact]
    public void Level_off_selects_nothing()
    {
        CategoryPageViewModel page = Build();

        Assert.Equal(Level.Off, page.Level);
        Assert.Empty(page.SelectedItems);
    }

    [Fact]
    public void The_level_dial_selects_entries_at_or_below_the_dial()
    {
        CategoryPageViewModel page = Build();

        page.Level = Level.Paranoid;

        Assert.NotEmpty(page.SelectedItems);
        Assert.All(page.SelectedItems, item =>
        {
            Assert.NotEqual(Level.Off, item.Level);
            Assert.True(item.Level <= Level.Paranoid);
        });
    }

    [Fact]
    public void Higher_levels_are_cumulative()
    {
        CategoryPageViewModel page = Build();

        page.Level = Level.Balanced;
        HashSet<TweakId> balanced = [.. page.SelectedItems.Select(i => i.Id)];
        page.Level = Level.Strict;
        HashSet<TweakId> strict = [.. page.SelectedItems.Select(i => i.Id)];

        Assert.True(balanced.IsSubsetOf(strict));
    }

    [Fact]
    public void Every_applicable_entry_is_shown_regardless_of_verification_status()
    {
        CategoryPageViewModel page = Build();

        // No draft gating in the browse UI: the visible list is every applicable,
        // non-deprecated entry, verified or not.
        Assert.Equal(page.AllItems.Count, page.Items.Count);
        Assert.Contains(page.Items, i => i.Status == TweakStatus.Draft);
    }

    [Fact]
    public void An_all_draft_section_still_lists_its_entries()
    {
        // Every Shell entry ships as Draft; they are shown, never hidden.
        CategoryPageViewModel page = Build(Category.Shell);

        Assert.NotEmpty(page.Items);
        Assert.False(page.IsEmpty);
        Assert.Equal(string.Empty, page.EmptyMessage);
    }

    [Fact]
    public void The_risk_filter_narrows_the_visible_list()
    {
        CategoryPageViewModel page = Build();

        page.RiskFilter = RiskTier.Safe;

        Assert.All(page.Items, item => Assert.Equal(RiskTier.Safe, item.Risk));
    }

    [Fact]
    public void Search_narrows_to_matching_entries_and_a_nonsense_term_empties_the_list()
    {
        CategoryPageViewModel page = Build();
        string idFragment = page.Items[0].IdText;

        page.SearchText = idFragment;
        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item => Assert.True(item.Matches(idFragment)));

        page.SearchText = "no-entry-matches-this-zzz";
        Assert.Empty(page.Items);
    }

    [Fact]
    public void Selection_persists_across_filtering()
    {
        CategoryPageViewModel page = Build();
        TweakItemViewModel item = page.AllItems[0];
        item.IsSelected = true;

        page.SearchText = "no-entry-matches-this-zzz";
        page.SearchText = string.Empty;

        Assert.True(item.IsSelected);
        Assert.Contains(item, page.SelectedItems);
    }

    [Fact]
    public void The_selection_summary_tracks_the_selected_count()
    {
        CategoryPageViewModel page = Build();

        page.AllItems[0].IsSelected = true;

        Assert.Contains("1", page.SelectionSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void Review_and_apply_is_disabled_until_something_is_selected()
    {
        CategoryPageViewModel page = Build();

        Assert.False(page.ReviewAndApplyCommand.CanExecute(null));
        page.AllItems[0].IsSelected = true;
        Assert.True(page.ReviewAndApplyCommand.CanExecute(null));
    }

    [Fact]
    public void Review_and_apply_launches_the_flow_with_the_selected_entries()
    {
        FakeApplyFlowLauncher launcher = new();
        CategoryPageViewModel page = Build(Category.Privacy, launcher);
        page.AllItems[0].IsSelected = true;
        page.AllItems[1].IsSelected = true;

        page.ReviewAndApplyCommand.Execute(null);

        Assert.Equal(1, launcher.LaunchCount);
        Assert.Equal(2, launcher.LastEntries!.Count);
        Assert.Equal(Scope.Machine, launcher.LastOptions!.Scope);
        Assert.True(launcher.LastOptions.CreateRestorePoint);
    }

    [Fact]
    public void The_restore_point_setting_flows_into_the_apply_options()
    {
        FakeApplyFlowLauncher launcher = new();
        CategoryPageViewModel page = Build(Category.Privacy, launcher, new AppSettings { CreateRestorePoint = false });
        page.AllItems[0].IsSelected = true;

        page.ReviewAndApplyCommand.Execute(null);

        Assert.False(launcher.LastOptions!.CreateRestorePoint);
    }

    [Fact]
    public void Offers_the_five_levels_and_the_risk_filter_options()
    {
        CategoryPageViewModel page = Build();

        Assert.Equal(5, page.LevelOptions.Count);
        Assert.Equal(Level.Off, page.LevelOptions[0].Value);
        Assert.Equal(5, page.RiskFilterOptions.Count);
        Assert.Null(page.RiskFilterOptions[0].Value);
    }

    [Fact]
    public void The_baseline_filter_is_offered_only_on_the_security_page()
    {
        Assert.False(Build(Category.Privacy).ShowBaselineFilter);
        Assert.Empty(Build(Category.Privacy).BaselineFilterOptions);

        CategoryPageViewModel security = Build(Category.Security);
        Assert.True(security.ShowBaselineFilter);
        // "All baselines" plus one option per built-in baseline.
        Assert.Equal(Baselines.Count + 1, security.BaselineFilterOptions.Count);
        Assert.Null(security.BaselineFilterOptions[0].Value);
    }

    [Fact]
    public void The_baseline_filter_narrows_the_list_to_the_baseline_tweaks()
    {
        CategoryPageViewModel page = Build(Category.Security);
        Baseline baseline = Baselines.First(b => string.Equals(b.Id, "disa-stig-v2r7", StringComparison.Ordinal));
        HashSet<TweakId> baselineIds = [.. baseline.Rules.SelectMany(r => r.TweakIds)];

        page.BaselineFilter = baseline;

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item => Assert.Contains(item.Id, baselineIds));
    }
}
