using System.Globalization;
using System.Net;
using OpenTheWindows.Core.Releases;

namespace OpenTheWindows.Core.Tests.Releases;

public sealed class GitHubAppReleaseCheckerTests
{
    private static readonly Uri Homepage = new("https://github.com/RandyNorthrup/open-the-windows", UriKind.Absolute);
    private const string Current = "0.1.0";

    [Fact]
    public async Task Reports_a_newer_release()
    {
        using var handler = new ScriptableHttpMessageHandler(HttpStatusCode.OK, Payload("v0.2.0"));
        AppReleaseCheckResult result = await CheckAsync(handler);

        Assert.True(result.Succeeded);
        Assert.True(result.UpdateAvailable);
        Assert.Equal("0.2.0", result.LatestVersion);
        Assert.NotNull(handler.LastRequestUri);
        Assert.Equal("api.github.com", handler.LastRequestUri.Host);
    }

    [Fact]
    public async Task Treats_matching_version_as_current()
    {
        using var handler = new ScriptableHttpMessageHandler(HttpStatusCode.OK, Payload("v0.1.0"));
        AppReleaseCheckResult result = await CheckAsync(handler);

        Assert.True(result.Succeeded);
        Assert.False(result.UpdateAvailable);
    }

    [Fact]
    public async Task Missing_release_is_success_without_an_update()
    {
        using var handler = new ScriptableHttpMessageHandler(HttpStatusCode.NotFound, "{}");
        AppReleaseCheckResult result = await CheckAsync(handler);

        Assert.True(result.Succeeded);
        Assert.False(result.UpdateAvailable);
        Assert.Contains("No GitHub release", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Draft_or_prerelease_is_ignored()
    {
        using var handler = new ScriptableHttpMessageHandler(HttpStatusCode.OK, Payload("v0.2.0", draft: true));
        AppReleaseCheckResult result = await CheckAsync(handler);

        Assert.True(result.Succeeded);
        Assert.False(result.UpdateAvailable);
        Assert.Contains("draft or prerelease", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Http_error_is_a_failed_check()
    {
        using var handler = new ScriptableHttpMessageHandler(HttpStatusCode.InternalServerError, "{}");
        AppReleaseCheckResult result = await CheckAsync(handler);

        Assert.False(result.Succeeded);
        Assert.False(result.UpdateAvailable);
        Assert.Contains("500", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_json_is_a_failed_check()
    {
        using var handler = new ScriptableHttpMessageHandler(HttpStatusCode.OK, "{not json");
        AppReleaseCheckResult result = await CheckAsync(handler);

        Assert.False(result.Succeeded);
        Assert.Contains("not valid JSON", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Network_failure_is_a_failed_check()
    {
        using var handler = new ScriptableHttpMessageHandler(new HttpRequestException("offline"));
        AppReleaseCheckResult result = await CheckAsync(handler);

        Assert.False(result.Succeeded);
        Assert.Contains("Could not reach GitHub", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Latest_release_api_is_derived_from_the_homepage()
    {
        Uri uri = GitHubAppReleaseChecker.LatestReleaseApi(Homepage);

        Assert.Equal("api.github.com", uri.Host);
        Assert.Equal("/repos/RandyNorthrup/open-the-windows/releases/latest", uri.AbsolutePath);
    }

    private static string Payload(string tag, bool draft = false)
        => string.Create(CultureInfo.InvariantCulture,
            $"{{\"tag_name\":\"{tag}\",\"html_url\":\"https://example.invalid/r\",\"draft\":{BoolJson(draft)},\"prerelease\":false}}");

    private static string BoolJson(bool value) => value ? "true" : "false";

    private static async Task<AppReleaseCheckResult> CheckAsync(ScriptableHttpMessageHandler handler)
    {
        using var client = new HttpClient(handler, disposeHandler: false);
        var checker = new GitHubAppReleaseChecker(client, Current, Homepage);
        return await checker.CheckAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
