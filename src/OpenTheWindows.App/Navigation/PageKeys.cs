namespace OpenTheWindows.App.Navigation;

/// <summary>
/// Stable identifiers for the shell's pages. Used as navigation keys and as the
/// prefix of every automation id on a page (<c>&lt;PageKey&gt;_&lt;Element&gt;</c>),
/// so they must not change once shipped.
/// </summary>
internal static class PageKeys
{
    public const string Dashboard = "Dashboard";
    public const string Privacy = "Privacy";
    public const string Updates = "Updates";
    public const string Security = "Security";
    public const string Performance = "Performance";
    public const string Debloat = "Debloat";
    public const string Shell = "Shell";
    public const string Profiles = "Profiles";
    public const string History = "History";
    public const string Settings = "Settings";
    public const string About = "About";
}
