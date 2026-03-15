using System.Text.Json;
using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode;

/// <summary>
/// Represents a single message part. The <see cref="Payload"/> field holds the full
/// part object as raw JSON because the <c>Part</c> TypeScript type is a discriminated
/// union of 11 subtypes — typed deserialization is deferred to a future spec.
/// </summary>
/// <param name="Id">The unique part identifier.</param>
/// <param name="SessionId">The ID of the session this part belongs to.</param>
/// <param name="MessageId">The ID of the message this part belongs to.</param>
/// <param name="Type">The part type discriminator (e.g. <c>text</c>, <c>tool</c>, <c>file</c>).</param>
/// <param name="Payload">The full part object as raw JSON for flexible downstream processing.</param>
public sealed record PartDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("sessionID")] string SessionId,
    [property: JsonPropertyName("messageID")] string MessageId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("payload")] JsonElement Payload
);

/// <summary>
/// Represents the common fields of a message (either a <c>UserMessage</c> or
/// <c>AssistantMessage</c>). Only the fields shared by both union members are typed;
/// role-specific fields can be accessed by re-deserializing from the parent
/// <see cref="MessageWithPartsDto"/> if needed in a future spec.
/// </summary>
/// <param name="Id">The unique message identifier.</param>
/// <param name="SessionId">The ID of the session this message belongs to.</param>
/// <param name="Role">The message role: <c>user</c> or <c>assistant</c>.</param>
/// <param name="Time">
/// The message timestamp object as raw JSON. The shape differs between
/// <c>UserMessage</c> (<c>{ created: number }</c>) and
/// <c>AssistantMessage</c> (<c>{ created: number, completed?: number }</c>),
/// so it is kept as a <see cref="JsonElement"/> for v1.
/// </param>
public sealed record MessageInfoDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("sessionID")] string SessionId,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("time")] JsonElement Time
);

/// <summary>
/// Combines a message's metadata with its associated parts.
/// </summary>
/// <param name="Info">The message metadata.</param>
/// <param name="Parts">The ordered list of parts that make up the message content.</param>
public sealed record MessageWithPartsDto(
    [property: JsonPropertyName("info")] MessageInfoDto Info,
    [property: JsonPropertyName("parts")] IReadOnlyList<PartDto> Parts
);
