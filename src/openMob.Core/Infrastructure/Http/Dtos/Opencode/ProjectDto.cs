using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode;

/// <summary>
/// Represents the time metadata for a project.
/// </summary>
/// <param name="Created">Unix timestamp (ms) when the project was created.</param>
/// <param name="Initialized">Unix timestamp (ms) when the project was initialized, or <c>null</c>.</param>
public sealed record ProjectTimeDto(
    [property: JsonPropertyName("created")] long Created,
    [property: JsonPropertyName("initialized")] long? Initialized
);

/// <summary>
/// Response DTO for a project entry. Maps the <c>Project</c> TypeScript type.
/// </summary>
/// <param name="Id">The unique project identifier.</param>
/// <param name="Worktree">The worktree path for this project.</param>
/// <param name="VcsDir">The VCS directory path, or <c>null</c> if not applicable.</param>
/// <param name="Vcs">The VCS type (e.g. <c>"git"</c>), or <c>null</c> if not detected.</param>
/// <param name="Time">Timestamps for project creation and initialization.</param>
public sealed record ProjectDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("worktree")] string Worktree,
    [property: JsonPropertyName("vcsDir")] string? VcsDir,
    [property: JsonPropertyName("vcs")] string? Vcs,
    [property: JsonPropertyName("time")] ProjectTimeDto Time
);
