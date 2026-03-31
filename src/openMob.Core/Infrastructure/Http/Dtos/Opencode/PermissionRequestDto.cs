using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode;

/// <summary>
/// Represents a pending permission request returned by <c>GET /permission</c>.
/// Maps to the server's <c>Permission.Request</c> schema.
/// </summary>
/// <param name="Id">The unique permission request identifier.</param>
/// <param name="SessionId">The session identifier this permission belongs to.</param>
/// <param name="Permission">The permission type being requested (e.g. <c>edit</c>).</param>
/// <param name="Patterns">The file or resource patterns the permission applies to.</param>
/// <param name="Always">Patterns that will be added to the approved ruleset when replied with <c>always</c>.</param>
public sealed record PermissionRequestDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("sessionID")] string SessionId,
    [property: JsonPropertyName("permission")] string Permission,
    [property: JsonPropertyName("patterns")] IReadOnlyList<string> Patterns,
    [property: JsonPropertyName("always")] IReadOnlyList<string> Always
);
