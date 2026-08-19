using OpenTheWindows.App.Resources;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core.Catalog;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.App.Tests.ViewModels;

/// <summary>Per-entry projection: labels, badges, search match and selection.</summary>
public sealed class TweakItemViewModelTests
{
    private static readonly TweakCatalog Catalog = CatalogLoader.LoadBuiltIn().Catalog!;

    private static TweakItemViewModel FirstItem() => new(Catalog.Entries.First(e => e.Category == Category.Privacy));

    [Fact]
    public void Projects_the_entry_fields()
    {
        TweakDefinition entry = Catalog.Entries.First(e => e.Category == Category.Privacy);
        TweakItemViewModel item = new(entry);

        Assert.Equal(entry.Id, item.Id);
        Assert.Equal(entry.Id.Value, item.IdText);
        Assert.Equal(entry.Title, item.Title);
        Assert.Equal(DisplayLabels.For(entry.Risk), item.RiskLabel);
        Assert.Equal(entry.Requires != RestartRequirement.None, item.RequiresRestart);
        Assert.NotEmpty(item.ActionSummaries);
    }

    [Fact]
    public void IsSelected_is_mutable_and_defaults_off()
    {
        TweakItemViewModel item = FirstItem();

        Assert.False(item.IsSelected);
        item.IsSelected = true;
        Assert.True(item.IsSelected);
    }

    [Fact]
    public void Matches_its_own_id_and_title_but_not_a_nonsense_term()
    {
        TweakItemViewModel item = FirstItem();

        Assert.True(item.Matches(item.IdText));
        Assert.True(item.Matches(item.Title));
        Assert.False(item.Matches("this-term-matches-no-entry-zzz"));
    }

    [Fact]
    public void Rejects_a_null_entry()
    {
        Assert.Throws<ArgumentNullException>(() => new TweakItemViewModel(null!));
    }
}
