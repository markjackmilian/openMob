using System.Text.Json;
using openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;

namespace openMob.Core.Helpers;

/// <summary>Builds <see cref="SendPromptRequest"/> instances from plain text input.</summary>
public sealed class SendPromptRequestBuilder
{
    /// <summary>
    /// Creates a <see cref="SendPromptRequest"/> with a single text part.
    /// The part is serialized as <c>{ "type": "text", "text": "&lt;text&gt;" }</c>
    /// matching the opencode wire format for a <c>TextPart</c>.
    /// </summary>
    /// <param name="text">The plain text content of the prompt.</param>
    /// <param name="modelId">
    /// The model ID without provider prefix (e.g. <c>"claude-3-5-haiku-20241022"</c>), or <c>null</c>
    /// to use the session/agent default. Must be paired with <paramref name="providerId"/>.
    /// </param>
    /// <param name="providerId">
    /// The provider ID (e.g. <c>"anthropic"</c>), or <c>null</c> to use the session/agent default.
    /// Must be paired with <paramref name="modelId"/>.
    /// </param>
    /// <param name="agentName">
    /// The agent name to use for this prompt, or <c>null</c> to use the project default.
    /// When <c>null</c>, the <c>"agent"</c> field is omitted from the serialized JSON.
    /// </param>
    /// <returns>A <see cref="SendPromptRequest"/> ready to send to the opencode server.</returns>
    public static SendPromptRequest FromText(
        string text,
        string? modelId = null,
        string? providerId = null,
        string? agentName = null)
    {
        // Serialize { "type": "text", "text": "<text>" } as a JsonElement
        var json = JsonSerializer.SerializeToElement(new { type = "text", text });

        // Build nested model object only when both parts are present.
        // The opencode server expects { "model": { "providerID": "...", "modelID": "..." } }
        // not flat top-level modelID/providerID fields.
        SendPromptModelRef? model = (modelId is not null && providerId is not null)
            ? new SendPromptModelRef(ProviderId: providerId, ModelId: modelId)
            : null;

        return new SendPromptRequest(
            Parts: [json],
            Model: model,
            Agent: agentName);
    }
}
