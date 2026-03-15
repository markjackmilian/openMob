using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode;

/// <summary>
/// Response DTO for the <c>GET /vcs</c> endpoint. Maps the <c>VcsInfo</c> TypeScript type.
/// </summary>
/// <param name="Branch">The current VCS branch name.</param>
public sealed record VcsInfoDto(
    [property: JsonPropertyName("branch")] string Branch
);
