using OpenTheWindows.App.Resources;
using OpenTheWindows.Core.Model;

namespace OpenTheWindows.App.Navigation;

/// <summary>
/// Maps the domain <see cref="Category"/> to the shell page that presents it —
/// its <see cref="PageKeys"/> key and navigation title. The five general
/// category pages share <c>CategoryPageViewModel</c>; <see cref="Category.Updates"/>
/// has its own page with the pause controls.
/// </summary>
internal static class CategoryPages
{
    /// <summary>The page key for a category.</summary>
    public static string KeyFor(Category category) => category switch
    {
        Category.Privacy => PageKeys.Privacy,
        Category.Updates => PageKeys.Updates,
        Category.Security => PageKeys.Security,
        Category.Performance => PageKeys.Performance,
        Category.Debloat => PageKeys.Debloat,
        Category.Shell => PageKeys.Shell,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "No page for this category."),
    };

    /// <summary>The navigation title for a category.</summary>
    public static string TitleFor(Category category) => category switch
    {
        Category.Privacy => Strings.Nav_Privacy,
        Category.Updates => Strings.Nav_Updates,
        Category.Security => Strings.Nav_Security,
        Category.Performance => Strings.Nav_Performance,
        Category.Debloat => Strings.Nav_Debloat,
        Category.Shell => Strings.Nav_Shell,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "No page for this category."),
    };
}
