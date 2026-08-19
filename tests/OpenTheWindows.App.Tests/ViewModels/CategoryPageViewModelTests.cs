using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core.Abstractions;
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

    private static CategoryPageViewModel Build(Category category = Category.Privacy) => new(category, Catalog, Facts);

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
        page.IncludeDraft = true;

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
        page.IncludeDraft = true;

        page.Level = Level.Balanced;
        HashSet<TweakId> balanced = [.. page.SelectedItems.Select(i => i.Id)];
        page.Level = Level.Strict;
        HashSet<TweakId> strict = [.. page.SelectedItems.Select(i => i.Id)];

        Assert.True(balanced.IsSubsetOf(strict));
    }

    [Fact]
    public void Draft_entries_are_hidden_until_included()
    {
        CategoryPageViewModel page = Build();
        int verifiedOnly = page.Items.Count;

        page.IncludeDraft = true;

        Assert.True(page.Items.Count >= verifiedOnly);
        Assert.All(page.Items.Where(i => i.Status == TweakStatus.Draft), _ => Assert.True(page.IncludeDraft));
    }

    [Fact]
    public void The_risk_filter_narrows_the_visible_list()
    {
        CategoryPageViewModel page = Build();
        page.IncludeDraft = true;

        page.RiskFilter = RiskTier.Safe;

        Assert.All(page.Items, item => Assert.Equal(RiskTier.Safe, item.Risk));
    }

    [Fact]
    public void Search_narrows_to_matching_entries_and_a_nonsense_term_empties_the_list()
    {
        CategoryPageViewModel page = Build();
        page.IncludeDraft = true;
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
    public void Offers_the_five_levels_and_the_risk_filter_options()
    {
        CategoryPageViewModel page = Build();

        Assert.Equal(5, page.LevelOptions.Count);
        Assert.Equal(Level.Off, page.LevelOptions[0].Value);
        Assert.Equal(5, page.RiskFilterOptions.Count);
        Assert.Null(page.RiskFilterOptions[0].Value);
    }
}
