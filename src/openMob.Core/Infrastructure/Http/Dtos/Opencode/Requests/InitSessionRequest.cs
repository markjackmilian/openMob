using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>Request body for <c>POST /session/{id}/init</c>.</summary>
/// <param name="MessageId">The message ID to initialize from, or <c>null</c>.</param>
public sealed record InitSessionRequest(
    [property: JsonPropertyName("messageID")] string? MessageId
);
