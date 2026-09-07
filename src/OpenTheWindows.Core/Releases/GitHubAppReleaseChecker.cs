using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace OpenTheWindows.Core.Releases;

/// <summary>
/// Opt-in GitHub Releases client. One HTTPS GET to the latest-release endpoint
/// derived from <see cref="AppInfo.HomepageUri"/>. Never called unless the user
/// asks.
/// </summary>
public sealed class GitHubAppReleaseChecker : IAppReleaseChecker
{
    /// <summary>How long one check may wait for GitHub.</summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private static readonly HttpClient ProcessClient = CreateClient();

    private readonly HttpClient _client;
    private readonly string _currentVersion;
    private readonly Uri _latestUri;

    /// <summary>Creates a checker over an injected client (tests supply a handler).</summary>
    public GitHubAppReleaseChecker(HttpClient client)
        : this(client, AppInfo.Version, new Uri(AppInfo.HomepageUri, UriKind.Absolute))
    {
    }

    /// <summary>Creates a checker with an explicit running version and homepage (tests).</summary>
    public GitHubAppReleaseChecker(HttpClient client, string currentVersion, Uri homepageUri)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentVersion);
        ArgumentNullException.ThrowIfNull(homepageUri);
        _client = client;
        _currentVersion = currentVersion;
        _latestUri = LatestReleaseApi(homepageUri);
    }

    /// <summary>Creates a checker over the process-wide GitHub client (CLI).</summary>
    public static GitHubAppReleaseChecker ForProcess() => new(ProcessClient);

    /// <summary>Builds an <see cref="HttpClient"/> with the GitHub-required user agent and the product timeout.</summary>
    public static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = RequestTimeout };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(AppInfo.CliName, AppInfo.Version));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    /// <inheritdoc />
    public async Task<AppReleaseCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _client.GetAsync(_latestUri, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Fail(string.Create(CultureInfo.InvariantCulture, $"Could not reach GitHub Releases: {ex.Message}"));
        }

        using (response)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new AppReleaseCheckResult(true, false, _currentVersion, null, null, "No GitHub release has been published yet.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return Fail(string.Create(CultureInfo.InvariantCulture,
                    $"GitHub Releases returned {(int)response.StatusCode} {response.ReasonPhrase}."));
            }

            string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            GitHubReleaseDto? dto;
            try
            {
                dto = JsonSerializer.Deserialize(body, GitHubReleaseJsonContext.Default.GitHubReleaseDto);
            }
            catch (JsonException ex)
            {
                return Fail(string.Create(CultureInfo.InvariantCulture, $"GitHub Releases payload was not valid JSON: {ex.Message}"));
            }

            if (dto is null || string.IsNullOrWhiteSpace(dto.TagName))
            {
                return Fail("GitHub Releases payload did not include a tag name.");
            }

            if (dto.Draft || dto.Prerelease)
            {
                return new AppReleaseCheckResult(true, false, _currentVersion, dto.TagName, TryUri(dto.HtmlUrl), "The latest GitHub tag is a draft or prerelease; it is ignored.");
            }

            string latest = StripLeadingV(dto.TagName);
            if (!Version.TryParse(NormalizeForParse(_currentVersion), out Version? current)
                || !Version.TryParse(NormalizeForParse(latest), out Version? published))
            {
                return Fail(string.Create(CultureInfo.InvariantCulture,
                    $"Could not compare versions (running {_currentVersion}, latest {latest})."));
            }

            bool newer = published > current;
            string message = newer
                ? string.Create(CultureInfo.InvariantCulture, $"A newer release is available: {latest} (running {_currentVersion}).")
                : string.Create(CultureInfo.InvariantCulture, $"Running {_currentVersion}; GitHub latest is {latest}.");
            return new AppReleaseCheckResult(true, newer, _currentVersion, latest, TryUri(dto.HtmlUrl), message);
        }
    }

    internal static Uri LatestReleaseApi(Uri homepageUri)
    {
        ArgumentNullException.ThrowIfNull(homepageUri);
        string repoPath = homepageUri.AbsolutePath.Trim('/');
        var builder = new UriBuilder
        {
            Scheme = Uri.UriSchemeHttps,
            Host = "api.github.com",
            Path = string.Create(CultureInfo.InvariantCulture, $"/repos/{repoPath}/releases/latest"),
        };
        return builder.Uri;
    }

    private AppReleaseCheckResult Fail(string message)
        => new(false, false, _currentVersion, null, null, message);

    private static string StripLeadingV(string tag)
        => tag.StartsWith('v') || tag.StartsWith('V') ? tag[1..] : tag;

    private static string NormalizeForParse(string version)
    {
        int plus = version.IndexOf('+', StringComparison.Ordinal);
        string core = plus > 0 ? version[..plus] : version;
        int dash = core.IndexOf('-', StringComparison.Ordinal);
        return dash > 0 ? core[..dash] : core;
    }

    private static Uri? TryUri(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ? uri : null;
}
