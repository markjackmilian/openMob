using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>Request body for <c>POST /provider/{id}/auth/callback</c>.</summary>
/// <param name="Code">The OAuth authorization code received from the provider.</param>
/// <param name="State">The OAuth state parameter for CSRF protection.</param>
public sealed record OAuthCallbackRequest(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("state")] string State
);
