using System.Text.Json;
using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>
/// Request body for <c>POST /provider/{id}/auth</c>.
/// The auth object is passed as raw JSON because the shape varies by auth type
/// (OAuth, API key, well-known).
/// </summary>
/// <param name="Auth">The authentication object as raw JSON.</param>
public sealed record SetProviderAuthRequest(
    [property: JsonPropertyName("auth")] JsonElement Auth
);
