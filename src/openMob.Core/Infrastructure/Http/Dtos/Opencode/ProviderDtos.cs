using System.Text.Json;
using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode;

/// <summary>
/// Envelope returned by <c>GET /provider</c>.
/// </summary>
/// <param name="All">All providers known to the server.</param>
/// <param name="Default">
/// A map of provider ID → default model ID for each provider that has a default configured.
/// </param>
/// <param name="Connected">
/// IDs of providers that are currently connected (i.e. have a valid API key or OAuth token).
/// Use this list to determine whether any provider is configured.
/// </param>
public sealed record ProviderListResponseDto(
    [property: JsonPropertyName("all")]       IReadOnlyList<ProviderDto>          All,
    [property: JsonPropertyName("default")]   IReadOnlyDictionary<string, string> Default,
    [property: JsonPropertyName("connected")] IReadOnlyList<string>               Connected
);

/// <summary>
/// Represents a single provider entry. Maps the <c>Provider</c> TypeScript type.
/// </summary>
/// <param name="Id">The provider identifier.</param>
/// <param name="Name">The display name of the provider.</param>
/// <param name="Source">The source of the provider: <c>env</c>, <c>config</c>, <c>custom</c>, or <c>api</c>.</param>
/// <param name="Env">Environment variable names used by this provider.</param>
/// <param name="Key">
/// The API key, if available. Not included in <c>GET /provider</c> responses;
/// use <see cref="ProviderListResponseDto.Connected"/> to check connectivity instead.
/// </param>
/// <param name="Options">Raw provider options (complex nested object).</param>
/// <param name="Models">Raw model definitions (too complex for typed deserialization in v1).</param>
public sealed record ProviderDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("env")] IReadOnlyList<string> Env,
    [property: JsonPropertyName("key")] string? Key,
    [property: JsonPropertyName("options")] JsonElement Options,
    [property: JsonPropertyName("models")] JsonElement Models
);

/// <summary>
/// Represents a single authentication method for a provider.
/// Maps the <c>ProviderAuthMethod</c> TypeScript type.
/// </summary>
/// <param name="Type">The authentication type: <c>oauth</c> or <c>api</c>.</param>
/// <param name="Label">A human-readable label for this auth method.</param>
public sealed record ProviderAuthMethodDto(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("label")] string Label
);

/// <summary>
/// Response DTO for OAuth authorization details.
/// Maps the <c>ProviderAuthAuthorization</c> TypeScript type.
/// </summary>
/// <param name="Url">The OAuth authorization URL to open in a browser.</param>
/// <param name="Method">The authorization method: <c>auto</c> or <c>code</c>.</param>
/// <param name="Instructions">Human-readable instructions for completing the OAuth flow.</param>
public sealed record ProviderAuthAuthorizationDto(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("instructions")] string Instructions
);
