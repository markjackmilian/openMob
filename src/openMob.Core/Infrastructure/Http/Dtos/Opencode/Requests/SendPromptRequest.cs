using System.Text.Json;
using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

/// <summary>
/// Request body for <c>POST /session/{id}/message</c> and <c>POST /session/{id}/prompt_async</c>.
/// </summary>
/// <param name="Parts">The raw parts array (each element is a typed part object as JSON).</param>
/// <param name="ModelId">The model ID to use for this prompt, or <c>null</c> to use the session default.</param>
/// <param name="ProviderId">The provider ID to use for this prompt, or <c>null</c> to use the session default.</param>
public sealed record SendPromptRequest(
    [property: JsonPropertyName("parts")] IReadOnlyList<JsonElement> Parts,
    [property: JsonPropertyName("modelID")] string? ModelId,
    [property: JsonPropertyName("providerID")] string? ProviderId
);
