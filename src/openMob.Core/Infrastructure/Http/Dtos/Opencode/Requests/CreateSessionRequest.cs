using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>Request body for <c>POST /session</c>.</summary>
/// <param name="Title">Optional display title for the new session.</param>
/// <param name="ParentId">Optional ID of the parent session for forked sessions.</param>
public sealed record CreateSessionRequest(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("parentID")] string? ParentId
);
