using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>Request body for <c>POST /session/{id}/shell</c>.</summary>
/// <param name="Command">The shell command to execute.</param>
public sealed record RunShellRequest(
    [property: JsonPropertyName("command")] string Command
);
