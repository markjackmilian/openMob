using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>Request body for <c>PUT /session/{id}</c>.</summary>
/// <param name="Title">The new title for the session, or <c>null</c> to leave unchanged.</param>
public sealed record UpdateSessionRequest(
    [property: JsonPropertyName("title")] string? Title
);
