namespace OpenTheWindows.Core.Tests;

public sealed class AppInfoTests
{
    [Fact]
    public void Title_and_cli_name_are_the_agreed_branding()
    {
        Assert.Equal("Open the Windows", AppInfo.Title);
        Assert.Equal("otw", AppInfo.CliName);
    }

    [Fact]
    public void Version_is_semver_like_without_build_metadata()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppInfo.Version));
        Assert.DoesNotContain('+', AppInfo.Version);
        Assert.Matches(@"^\d+\.\d+\.\d+", AppInfo.Version);
    }
}
