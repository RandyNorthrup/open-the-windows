using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.Resources;
using OpenTheWindows.Core;

namespace OpenTheWindows.App.ViewModels;

/// <summary>
/// The About page: product identity, version, homepage and the trademark /
/// non-affiliation notice required by the research (report 02 §9 trademark
/// hygiene).
/// </summary>
internal sealed class AboutViewModel : IPageViewModel
{
    /// <inheritdoc />
    public string Key => PageKeys.About;

    /// <inheritdoc />
    public string Title => Strings.Nav_About;

    /// <summary>The product name.</summary>
    public string ProductName { get; } = AppInfo.Title;

    /// <summary>The informational version string.</summary>
    public string Version { get; } = AppInfo.Version;

    /// <summary>The project homepage URL.</summary>
    public string Homepage { get; } = AppInfo.HomepageUri;

    /// <summary>The licence identifier.</summary>
    public string License { get; } = "MIT";

    /// <summary>The trademark and non-affiliation disclaimer.</summary>
    public string Disclaimer { get; } =
        "Open the Windows is not affiliated with, endorsed by, or sponsored by " +
        "Microsoft. “Windows” and “Microsoft” are trademarks of " +
        "Microsoft Corporation, used here only to describe what this tool configures.";
}
