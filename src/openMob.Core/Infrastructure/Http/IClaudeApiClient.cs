using openMob.Core.Infrastructure.Http.Dtos;

namespace openMob.Core.Infrastructure.Http;

/// <summary>
/// Typed HTTP client interface for communicating with the opencode server API.
/// </summary>
public interface IClaudeApiClient
{
    /// <summary>Checks whether the opencode server is reachable and healthy.</summary>
    Task<bool> CheckHealthAsync(CancellationToken ct = default);

    /// <summary>Retrieves all available sessions from the server.</summary>
    Task<IReadOnlyList<SessionDto>> GetSessionsAsync(CancellationToken ct = default);

    /// <summary>Creates a new session on the server.</summary>
    Task<SessionDto> CreateSessionAsync(string? title, CancellationToken ct = default);

    /// <summary>Sends a message within an existing session.</summary>
    Task<MessageDto> SendMessageAsync(string sessionId, string content, CancellationToken ct = default);

    /// <summary>Opens a server-sent event stream and yields events as they arrive.</summary>
    IAsyncEnumerable<ServerEventDto> StreamEventsAsync(CancellationToken ct = default);
}
