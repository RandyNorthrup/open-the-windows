using OpenTheWindows.App.Navigation;
using OpenTheWindows.App.ViewModels;
using OpenTheWindows.Core;
using OpenTheWindows.Core.Releases;
using OpenTheWindows.TestSupport.Fakes;

namespace OpenTheWindows.App.Tests.ViewModels;

/// <summary>The About page surfaces product identity and the trademark disclaimer.</summary>
public sealed class AboutViewModelTests
{
    private readonly FakeAppReleaseChecker _checker = new();
    private AboutViewModel About => new(_checker);

    [Fact]
    public void Is_the_about_page()
    {
        AboutViewModel page = About;
        Assert.Equal(PageKeys.About, page.Key);
        Assert.False(string.IsNullOrEmpty(page.Title));
    }

    [Fact]
    public void Reports_product_identity()
    {
        AboutViewModel page = About;
        Assert.Equal(AppInfo.Title, page.ProductName);
        Assert.Equal(AppInfo.Version, page.Version);
        Assert.Equal(AppInfo.HomepageUri, page.Homepage);
        Assert.Equal("MIT", page.License);
    }

    [Fact]
    public void Carries_a_non_affiliation_disclaimer()
    {
        Assert.Contains("not affiliated", About.Disclaimer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Check_for_updates_writes_the_checker_message()
    {
        _checker.Result = new AppReleaseCheckResult(true, true, "0.1.0", "0.2.0", new Uri("https://example.invalid/r"), "A newer release is available.");
        AboutViewModel page = About;

        await page.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal(1, _checker.CallCount);
        Assert.Contains("newer", page.UpdateStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Does_not_query_github_until_asked()
    {
        AboutViewModel page = About;

        Assert.Equal(0, _checker.CallCount);
        Assert.Contains("Not checked", page.UpdateStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_a_null_checker()
        => Assert.Throws<ArgumentNullException>(() => new AboutViewModel(null!));
}
