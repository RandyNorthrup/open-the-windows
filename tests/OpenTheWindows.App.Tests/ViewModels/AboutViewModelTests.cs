using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core;

namespace OpenTheWindows.App.Tests.ViewModels;

/// <summary>The About page surfaces product identity and the trademark disclaimer.</summary>
public sealed class AboutViewModelTests
{
    private readonly AboutViewModel _about = new();

    [Fact]
    public void Is_the_about_page()
    {
        Assert.Equal(PageKeys.About, _about.Key);
        Assert.False(string.IsNullOrEmpty(_about.Title));
    }

    [Fact]
    public void Reports_product_identity()
    {
        Assert.Equal(AppInfo.Title, _about.ProductName);
        Assert.Equal(AppInfo.Version, _about.Version);
        Assert.Equal(AppInfo.HomepageUri, _about.Homepage);
        Assert.Equal("MIT", _about.License);
    }

    [Fact]
    public void Carries_a_non_affiliation_disclaimer()
    {
        Assert.Contains("not affiliated", _about.Disclaimer, StringComparison.OrdinalIgnoreCase);
    }
}
