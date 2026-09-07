using System.Net;

namespace OpenTheWindows.Core.Tests.Releases;

/// <summary>Returns a canned HTTP response, or throws, for GitHub release-checker tests.</summary>
internal sealed class ScriptableHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;
    private readonly Exception? _exception;

    public ScriptableHttpMessageHandler(HttpStatusCode status, string body)
    {
        _status = status;
        _body = body;
    }

    public ScriptableHttpMessageHandler(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _exception = exception;
        _body = string.Empty;
    }

    /// <summary>The request URI the handler saw.</summary>
    public Uri? LastRequestUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        if (_exception is not null)
        {
            throw _exception;
        }

        return Task.FromResult(new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body),
        });
    }
}
