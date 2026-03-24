using System.Text.Json;
using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>
/// Request body for <c>POST /session/{id}/message</c> and <c>POST /session/{id}/prompt_async</c>.
/// </summary>
/// <param name="Parts">The raw parts array (each element is a typed part object as JSON).</param>
/// <param name="ModelId">The model ID to use for this prompt, or <c>null</c> to use the session default.</param>
/// <param name="ProviderId">The provider ID to use for this prompt, or <c>null</c> to use the session default.</param>
/// <param name="Agent">The agent name to use for this prompt, or <c>null</c> to use the project default.
/// When <c>null</c>, the field is omitted from the serialized JSON so the server-side default is preserved.</param>
public sealed record SendPromptRequest(
    [property: JsonPropertyName("parts")] IReadOnlyList<JsonElement> Parts,
    [property: JsonPropertyName("modelID")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ModelId,
    [property: JsonPropertyName("providerID")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ProviderId,
    [property: JsonPropertyName("agent")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Agent = null
);
