using System.Text.Json;
using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>
/// Nested model object for <see cref="SendPromptRequest"/>.
/// The opencode server expects model as a nested object <c>{ "providerID": "...", "modelID": "..." }</c>,
/// not as flat top-level fields.
/// </summary>
/// <param name="ProviderId">The provider ID (e.g. <c>"anthropic"</c>, <c>"openai"</c>).</param>
/// <param name="ModelId">The model ID without provider prefix (e.g. <c>"claude-3-5-haiku-20241022"</c>).</param>
public sealed record SendPromptModelRef(
    [property: JsonPropertyName("providerID")] string ProviderId,
    [property: JsonPropertyName("modelID")] string ModelId
);

/// <summary>
/// Request body for <c>POST /session/{id}/message</c> and <c>POST /session/{id}/prompt_async</c>.
/// Wire format confirmed from opencode server source (<c>SessionPrompt.PromptInput</c> zod schema):
/// model is a nested object, not flat fields.
/// </summary>
/// <param name="Parts">The raw parts array (each element is a typed part object as JSON).</param>
/// <param name="Model">
/// The model to use for this prompt as a nested object, or <c>null</c> to use the session/agent default.
/// When <c>null</c>, the field is omitted from the serialized JSON so the server-side default is preserved.
/// </param>
/// <param name="Agent">The agent name to use for this prompt, or <c>null</c> to use the project default.
/// When <c>null</c>, the field is omitted from the serialized JSON so the server-side default is preserved.</param>
public sealed record SendPromptRequest(
    [property: JsonPropertyName("parts")] IReadOnlyList<JsonElement> Parts,
    [property: JsonPropertyName("model")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    SendPromptModelRef? Model,
    [property: JsonPropertyName("agent")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Agent = null
);
