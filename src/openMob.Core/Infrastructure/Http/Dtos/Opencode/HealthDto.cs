using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode;

/// <summary>
/// Response DTO for the <c>GET /global/health</c> endpoint.
/// </summary>
/// <param name="Healthy">Whether the server is healthy.</param>
/// <param name="Version">The server version string.</param>
public sealed record HealthDto(
    [property: JsonPropertyName("healthy")] bool Healthy,
    [property: JsonPropertyName("version")] string Version
);
