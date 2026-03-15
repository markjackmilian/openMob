using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>Request body for <c>POST /session/{id}/fork</c>.</summary>
/// <param name="MessageId">The message ID to fork from, or <c>null</c> to fork from the latest.</param>
/// <param name="Title">Optional title for the forked session.</param>
public sealed record ForkSessionRequest(
    [property: JsonPropertyName("messageID")] string? MessageId,
    [property: JsonPropertyName("title")] string? Title
);
