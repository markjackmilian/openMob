using System.Text.Json;
using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode;

/// <summary>
/// Represents a server-sent event received from the <c>GET /global/event</c> SSE stream.
/// </summary>
/// <remarks>
/// The <see cref="Data"/> field is a raw <see cref="JsonElement"/> because event payloads
/// vary by <see cref="EventType"/>. The first event received is always
/// <c>server.connected</c>.
/// </remarks>
/// <param name="EventType">The event type discriminator (e.g. <c>server.connected</c>, <c>message.updated</c>).</param>
/// <param name="EventId">The SSE event ID, or <c>null</c> if not provided by the server.</param>
/// <param name="Data">The raw JSON payload of the event, or <c>null</c> if the event has no data.</param>
public sealed record OpencodeEventDto(
    [property: JsonPropertyName("type")] string EventType,
    [property: JsonPropertyName("id")] string? EventId,
    [property: JsonPropertyName("data")] JsonElement? Data
);
