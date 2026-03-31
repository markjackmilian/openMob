using System.Text.Json;
using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>
/// Request body for <c>PATCH /config</c>.
/// The config object is passed as raw JSON. The server performs a merge —
/// only the keys present in the payload are overwritten.
/// </summary>
/// <param name="Config">The configuration object as raw JSON.</param>
public sealed record UpdateConfigRequest(
    [property: JsonPropertyName("config")] JsonElement Config
);
