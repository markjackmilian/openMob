using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>Request body for <c>POST /permission/{requestId}/reply</c>.</summary>
/// <param name="Reply">The permission reply value: <c>once</c>, <c>always</c>, or <c>reject</c>.</param>
public sealed record PermissionReplyRequest(
    [property: JsonPropertyName("reply")] string Reply
);
