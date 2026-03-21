using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>Request body for <c>POST /session</c>.</summary>
/// <param name="Title">Display title for the new session. Use <see cref="string.Empty"/> for no title — the server rejects <c>null</c>.</param>
/// <param name="ParentId">ID of the parent session for forked sessions. Use <see cref="string.Empty"/> when not forking — the server rejects <c>null</c>.</param>
public sealed record CreateSessionRequest(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("parentID")] string ParentId
);
