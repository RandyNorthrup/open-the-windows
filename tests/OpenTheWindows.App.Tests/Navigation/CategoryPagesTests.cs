using OpenTheWindows.App.Navigation;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.App.Tests.Navigation;

/// <summary>Every category maps to a page key and a non-empty title.</summary>
public sealed class CategoryPagesTests
{
    [Theory]
    [InlineData(Category.Privacy, PageKeys.Privacy)]
    [InlineData(Category.Updates, PageKeys.Updates)]
    [InlineData(Category.Security, PageKeys.Security)]
    [InlineData(Category.Performance, PageKeys.Performance)]
    [InlineData(Category.Debloat, PageKeys.Debloat)]
    [InlineData(Category.Shell, PageKeys.Shell)]
    public void Maps_each_category_to_its_page(Category category, string expectedKey)
    {
        Assert.Equal(expectedKey, CategoryPages.KeyFor(category));
        Assert.False(string.IsNullOrEmpty(CategoryPages.TitleFor(category)));
    }
}
