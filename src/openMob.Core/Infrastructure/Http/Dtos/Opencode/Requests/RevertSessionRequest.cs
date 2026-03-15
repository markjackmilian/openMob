using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>Request body for <c>POST /session/{id}/revert</c>.</summary>
/// <param name="MessageId">The ID of the message to revert to.</param>
/// <param name="PartId">The ID of the part to revert to, or <c>null</c>.</param>
public sealed record RevertSessionRequest(
    [property: JsonPropertyName("messageID")] string MessageId,
    [property: JsonPropertyName("partID")] string? PartId
);
