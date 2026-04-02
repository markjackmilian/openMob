namespace openMob.Core.Infrastructure.Http;

/// <summary>
/// Represents the current connectivity state of the opencode server connection.
/// </summary>
public enum ServerConnectionStatus
{
    /// <summary>No active server connection is configured or the connection has been lost.</summary>
    Disconnected,

    /// <summary>The client is attempting to connect or retry a failed request.</summary>
    Connecting,

    /// <summary>The server is reachable and the connection is healthy.</summary>
    Connected,

    /// <summary>The connection attempt failed after all retries were exhausted.</summary>
    Error,
}

/// <summary>
/// Manages the active opencode server connection, providing the base URL and
/// HTTP Basic Auth header for outgoing requests.
/// </summary>
public interface IOpencodeConnectionManager
{
    /// <summary>Gets the current connection status.</summary>
    ServerConnectionStatus ConnectionStatus { get; }

    /// <summary>
    /// Raised whenever <see cref="ConnectionStatus"/> changes.
    /// Subscribers receive the new status value.
    /// </summary>
    event Action<ServerConnectionStatus>? StatusChanged;

    /// <summary>
    /// Returns the base URL for the active server connection (e.g. <c>https://host</c> or
    /// <c>http://host:4096</c>), or <c>null</c> if no active connection is configured.
    /// The scheme is <c>https</c> when <see cref="ServerConnectionDto.UseHttps"/> is <c>true</c>;
    /// otherwise <c>http</c>. The port is omitted when it equals the protocol default (443 for HTTPS, 80 for HTTP).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<string?> GetBaseUrlAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns the HTTP Basic Auth header value in the form
    /// <c>Basic {base64(username:password)}</c>, or <c>null</c> if no credentials
    /// are configured for the active connection.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<string?> GetBasicAuthHeaderAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks whether the active server is reachable by calling <c>GET /global/health</c>.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the server responds with HTTP 200; <c>false</c> otherwise.</returns>
    Task<bool> IsServerReachableAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets the connection status directly. Called by <see cref="IOpencodeApiClient"/>
    /// during retry cycles to reflect the current connectivity state.
    /// </summary>
    /// <param name="status">The new status to apply.</param>
    void SetConnectionStatus(ServerConnectionStatus status);

    /// <summary>
    /// Returns the display name of the currently active server connection, or <c>null</c> if none is configured.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<string?> GetActiveServerNameAsync(CancellationToken ct = default);
}
