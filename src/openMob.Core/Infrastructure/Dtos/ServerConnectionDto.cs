namespace openMob.Core.Infrastructure.Dtos;

/// <summary>
/// Data transfer object for a server connection.
/// Does not contain the password — only a <see cref="HasPassword"/> flag
/// indicating whether a credential exists in secure storage.
/// </summary>
/// <param name="Id">The unique identifier (ULID format).</param>
/// <param name="Name">The user-defined display label.</param>
/// <param name="Host">The hostname or IP address of the opencode server.</param>
/// <param name="Port">The port number.</param>
/// <param name="Username">The optional username for Basic Auth, or null.</param>
/// <param name="IsActive">Whether this is the currently active connection.</param>
/// <param name="DiscoveredViaMdns">Whether this connection was discovered via mDNS.</param>
/// <param name="CreatedAt">The UTC timestamp when the record was created.</param>
/// <param name="UpdatedAt">The UTC timestamp when the record was last updated.</param>
/// <param name="HasPassword">True if a password exists in secure storage for this connection.</param>
public sealed record ServerConnectionDto(
    string Id,
    string Name,
    string Host,
    int Port,
    string? Username,
    bool IsActive,
    bool DiscoveredViaMdns,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool HasPassword
);
