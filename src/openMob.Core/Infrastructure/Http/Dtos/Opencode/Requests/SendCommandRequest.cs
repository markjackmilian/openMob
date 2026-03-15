using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>Request body for <c>POST /session/{id}/command</c>.</summary>
/// <param name="Name">The command name to execute.</param>
/// <param name="Arguments">Optional arguments to pass to the command.</param>
public sealed record SendCommandRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("arguments")] string? Arguments
);
