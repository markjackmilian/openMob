using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>Request body for <c>POST /session/{id}/summarize</c>.</summary>
/// <param name="MessageId">The message ID up to which to summarize, or <c>null</c> for the full session.</param>
public sealed record SummarizeSessionRequest(
    [property: JsonPropertyName("messageID")] string? MessageId
);
