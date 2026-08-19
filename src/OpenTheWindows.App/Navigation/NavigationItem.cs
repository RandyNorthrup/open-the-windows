namespace OpenTheWindows.App.Navigation;

/// <summary>
/// One entry in the shell's left navigation list: the <paramref name="Key"/>
/// the <see cref="Services.INavigationService"/> navigates to and the
/// human-readable <paramref name="Title"/> shown to the user.
/// </summary>
/// <param name="Key">A <see cref="PageKeys"/> value.</param>
/// <param name="Title">The label shown in the navigation list.</param>
internal sealed record NavigationItem(string Key, string Title);
