namespace openMob.Core.Data.Entities;

/// <summary>
/// EF Core entity representing a saved opencode server connection.
/// </summary>
/// <remarks>
/// The password is <b>never</b> stored in this entity or the database.
/// Credentials are managed exclusively through <see cref="Infrastructure.Security.IServerCredentialStore"/>
/// using platform-specific secure storage (iOS Keychain / Android EncryptedSharedPreferences).
/// </remarks>
public sealed class ServerConnection
{
    /// <summary>Gets or sets the unique identifier (ULID format).</summary>
    public string Id { get; set; } = Ulid.NewUlid().ToString();

    /// <summary>Gets or sets the user-defined display label for this connection.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the hostname or IP address of the opencode server.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>Gets or sets the port number. Defaults to 4096.</summary>
    public int Port { get; set; } = 4096;

    /// <summary>Gets or sets the optional username for Basic Auth. Null when auth is disabled.</summary>
    public string? Username { get; set; }

    /// <summary>Gets or sets whether this is the currently active connection. Only one record may be active at a time.</summary>
    public bool IsActive { get; set; }

    /// <summary>Gets or sets whether this connection was created via mDNS discovery.</summary>
    public bool DiscoveredViaMdns { get; set; }

    /// <summary>Gets or sets whether this connection uses HTTPS. Defaults to false.</summary>
    public bool UseHttps { get; set; } = false;

    /// <summary>Gets or sets the UTC timestamp when this record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the UTC timestamp when this record was last updated.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Gets or sets the default AI model ID for this server (format: "providerId/modelId"), or null if not set.</summary>
    public string? DefaultModelId { get; set; }
}
