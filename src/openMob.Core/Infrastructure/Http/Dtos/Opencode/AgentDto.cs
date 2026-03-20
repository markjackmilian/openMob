using System.Text.Json;
using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode;

/// <summary>
/// Response DTO for an agent entry. Maps the <c>Agent</c> TypeScript type.
/// </summary>
/// <param name="Name">The agent name.</param>
/// <param name="Description">An optional description of the agent.</param>
/// <param name="Mode">The agent mode: <c>subagent</c>, <c>primary</c>, or <c>all</c>.</param>
/// <param name="BuiltIn">Whether this is a built-in agent.</param>
/// <param name="TopP">The top-p sampling parameter, or <c>null</c>.</param>
/// <param name="Temperature">The temperature sampling parameter, or <c>null</c>.</param>
/// <param name="Color">The hex color code for the agent (e.g. <c>#FF5733</c>), or <c>null</c>.</param>
/// <param name="Model">The model configuration as raw JSON, or <c>null</c>.</param>
/// <param name="Prompt">The system prompt for this agent, or <c>null</c>.</param>
/// <param name="Tools">Raw tool configuration (map of tool name to enabled flag).</param>
/// <param name="Options">Raw additional options.</param>
/// <param name="MaxSteps">Maximum number of agentic iterations, or <c>null</c>.</param>
/// <param name="Permission">Raw permission configuration.</param>
/// <param name="Hidden">Whether this agent is hidden from user-facing pickers. Hidden agents run automatically and are not selectable by the user.</param>
public sealed record AgentDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("builtIn")] bool BuiltIn,
    [property: JsonPropertyName("topP")] double? TopP,
    [property: JsonPropertyName("temperature")] double? Temperature,
    [property: JsonPropertyName("color")] string? Color,
    [property: JsonPropertyName("model")] JsonElement? Model,
    [property: JsonPropertyName("prompt")] string? Prompt,
    [property: JsonPropertyName("tools")] JsonElement Tools,
    [property: JsonPropertyName("options")] JsonElement Options,
    [property: JsonPropertyName("maxSteps")] int? MaxSteps,
    [property: JsonPropertyName("permission")] JsonElement Permission,
    [property: JsonPropertyName("hidden")] bool Hidden
);
