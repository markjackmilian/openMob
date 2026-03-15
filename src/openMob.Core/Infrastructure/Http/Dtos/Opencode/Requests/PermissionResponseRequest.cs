using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>Request body for <c>POST /session/{id}/permission/{permissionId}</c>.</summary>
/// <param name="Response">The permission response: typically <c>allow</c> or <c>deny</c>.</param>
public sealed record PermissionResponseRequest(
    [property: JsonPropertyName("response")] string Response
);
