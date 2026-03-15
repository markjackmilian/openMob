using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>Request parameters for <c>GET /file</c> (find files by pattern).</summary>
/// <param name="Pattern">The glob pattern to match file names against, or <c>null</c>.</param>
/// <param name="Path">The directory path to search within, or <c>null</c> for the project root.</param>
public sealed record FindFilesRequest(
    [property: JsonPropertyName("pattern")] string? Pattern,
    [property: JsonPropertyName("path")] string? Path
);
