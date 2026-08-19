using OpenTheWindows.App.Resources;

namespace OpenTheWindows.App.Navigation;

/// <summary>
/// The ordered set of pages the shell shows in its navigation list. This is the
/// single source of truth for "which pages exist"; each entry must have a
/// matching keyed <see cref="ViewModels.IPageViewModel"/> registration and a
/// view <c>DataTemplate</c>. Pages are added here as milestones deliver them.
/// </summary>
internal static class ShellNavigation
{
    /// <summary>The navigation entries, in display order.</summary>
    public static IReadOnlyList<NavigationItem> Pages { get; } =
    [
        new(PageKeys.Dashboard, Strings.Nav_Dashboard),
        new(PageKeys.Privacy, Strings.Nav_Privacy),
        new(PageKeys.Security, Strings.Nav_Security),
        new(PageKeys.Performance, Strings.Nav_Performance),
        new(PageKeys.Debloat, Strings.Nav_Debloat),
        new(PageKeys.Shell, Strings.Nav_Shell),
        new(PageKeys.About, Strings.Nav_About),
    ];
}
