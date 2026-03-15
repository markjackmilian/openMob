using System.Net;
using System.Net.Http;
using System.Text;
using openMob.Core.Infrastructure.Settings;

namespace openMob.Tests.Helpers;

/// <summary>
/// A test double for <see cref="HttpMessageHandler"/> that intercepts HTTP calls
/// and returns pre-configured responses without making real network requests.
/// </summary>
/// <remarks>
/// Configure responses with <see cref="SetupResponse"/> before creating the
/// <see cref="HttpClient"/>. Inspect <see cref="CallCount"/> after the test to
/// verify how many times each endpoint was called (useful for retry verification).
/// </remarks>
internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode StatusCode, string Body)> _responses = new();

    /// <summary>
    /// Tracks how many times each endpoint was called.
    /// Key format: <c>"METHOD /path"</c> (e.g. <c>"GET /global/health"</c>).
    /// </summary>
    public Dictionary<string, int> CallCount { get; } = new();

    /// <summary>
    /// Stores the last request received, for header inspection in auth tests.
    /// </summary>
    public HttpRequestMessage? LastRequest { get; private set; }

    /// <summary>
    /// When set, the handler will pause before returning a response until this
    /// <see cref="TaskCompletionSource"/> is completed. Used for in-flight state tests.
    /// </summary>
    public TaskCompletionSource<bool>? PauseSource { get; set; }

    /// <summary>
    /// Configures the response for a given HTTP method and path.
    /// </summary>
    /// <param name="method">The HTTP method (e.g. <c>"GET"</c>, <c>"POST"</c>).</param>
    /// <param name="path">The absolute path (e.g. <c>"/global/health"</c>).</param>
    /// <param name="statusCode">The HTTP status code to return.</param>
    /// <param name="body">The response body string.</param>
    public void SetupResponse(string method, string path, HttpStatusCode statusCode, string body)
        => _responses[$"{method} {path}"] = (statusCode, body);

    /// <summary>
    /// Configures the handler to throw an <see cref="HttpRequestException"/> for a given endpoint.
    /// </summary>
    public void SetupException(string method, string path)
        => _responses[$"{method} {path}"] = (HttpStatusCode.ServiceUnavailable, "__THROW__");

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;

        var key = $"{request.Method.Method} {request.RequestUri!.AbsolutePath}";

        // Track call count
        CallCount.TryGetValue(key, out var count);
        CallCount[key] = count + 1;

        // Pause if requested (for in-flight state tests)
        if (PauseSource is not null)
            await PauseSource.Task.ConfigureAwait(false);

        if (_responses.TryGetValue(key, out var setup))
        {
            if (setup.Body == "__THROW__")
                throw new HttpRequestException($"Simulated network failure for {key}");

            var response = new HttpResponseMessage(setup.StatusCode)
            {
                Content = new StringContent(setup.Body, Encoding.UTF8, "application/json"),
            };
            return response;
        }

        // No matching setup — return 404
        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
        };
    }
}

/// <summary>
/// A test-only implementation of <see cref="IOpencodeSettingsService"/> that stores
/// the timeout in memory. Avoids any MAUI <c>Preferences</c> dependency.
/// </summary>
internal sealed class FakeOpencodeSettingsService : IOpencodeSettingsService
{
    /// <summary>Gets or sets the timeout in seconds. Defaults to <c>120</c>.</summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <inheritdoc />
    public int GetTimeoutSeconds() => TimeoutSeconds;

    /// <inheritdoc />
    public Task SetTimeoutSecondsAsync(int value, CancellationToken ct = default)
    {
        TimeoutSeconds = value;
        return Task.CompletedTask;
    }
}
