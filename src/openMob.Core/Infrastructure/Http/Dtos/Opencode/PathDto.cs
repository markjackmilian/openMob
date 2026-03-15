using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode;

/// <summary>
/// Response DTO for the <c>GET /path</c> endpoint. Maps the <c>Path</c> TypeScript type.
/// </summary>
/// <param name="State">Path to the application state directory.</param>
/// <param name="Config">Path to the configuration directory.</param>
/// <param name="Worktree">Path to the current worktree.</param>
/// <param name="Directory">Path to the current working directory.</param>
public sealed record PathDto(
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("config")] string Config,
    [property: JsonPropertyName("worktree")] string Worktree,
    [property: JsonPropertyName("directory")] string Directory
);
