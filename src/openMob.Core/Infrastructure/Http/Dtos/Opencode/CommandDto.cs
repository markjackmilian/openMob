using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode;

/// <summary>
/// Response DTO for a command entry. Maps the <c>Command</c> TypeScript type.
/// </summary>
/// <param name="Name">The command name (used as the slash-command identifier).</param>
/// <param name="Description">An optional human-readable description of the command.</param>
/// <param name="Agent">The agent to use when running this command, or <c>null</c>.</param>
/// <param name="Model">The model to use when running this command, or <c>null</c>.</param>
/// <param name="Template">The prompt template for this command.</param>
/// <param name="Subtask">Whether this command runs as a subtask, or <c>null</c> if not specified.</param>
public sealed record CommandDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("agent")] string? Agent,
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("template")] string Template,
    [property: JsonPropertyName("subtask")] bool? Subtask
);
