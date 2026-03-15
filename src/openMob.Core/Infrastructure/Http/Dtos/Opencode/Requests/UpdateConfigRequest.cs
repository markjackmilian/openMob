using System.Text.Json;
using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>
/// Request body for <c>PUT /config</c>.
/// The full config object is passed as raw JSON to allow partial updates
/// without requiring all fields to be present.
/// </summary>
/// <param name="Config">The configuration object as raw JSON.</param>
public sealed record UpdateConfigRequest(
    [property: JsonPropertyName("config")] JsonElement Config
);
