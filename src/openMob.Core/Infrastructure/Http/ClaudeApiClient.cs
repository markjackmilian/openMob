using openMob.Core.Infrastructure.Http.Dtos;

namespace openMob.Core.Infrastructure.Http;

/// <summary>
/// Typed HTTP client implementation for the opencode server API.
/// Method bodies are scaffolded — full implementation will be added in subsequent features.
/// </summary>
internal sealed class ClaudeApiClient : IClaudeApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>Initialises the client with the given HTTP client factory.</summary>
    public ClaudeApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public Task<bool> CheckHealthAsync(CancellationToken ct = default)
        => throw new NotImplementedException("Health check will be implemented in the server connectivity feature.");

    /// <inheritdoc />
    public Task<IReadOnlyList<SessionDto>> GetSessionsAsync(CancellationToken ct = default)
        => throw new NotImplementedException("Session listing will be implemented in the session management feature.");

    /// <inheritdoc />
    public Task<SessionDto> CreateSessionAsync(string? title, CancellationToken ct = default)
        => throw new NotImplementedException("Session creation will be implemented in the session management feature.");

    /// <inheritdoc />
    public Task<MessageDto> SendMessageAsync(string sessionId, string content, CancellationToken ct = default)
        => throw new NotImplementedException("Message sending will be implemented in the messaging feature.");

    /// <inheritdoc />
    public IAsyncEnumerable<ServerEventDto> StreamEventsAsync(CancellationToken ct = default)
        => throw new NotImplementedException("SSE streaming will be implemented in the streaming feature.");
}
