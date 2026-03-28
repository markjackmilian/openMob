using System.Text.Json;
using System.Text.Json.Serialization;

namespace openMob.Core.Infrastructure.Http.Dtos.Opencode;

/// <summary>
/// Represents a single message part. The opencode server returns parts as a
/// discriminated union — the common fields (<c>id</c>, <c>sessionID</c>,
/// <c>messageID</c>, <c>type</c>) are always present, while type-specific
/// fields vary. For <c>type: "text"</c> parts, the text content is in the
/// <c>text</c> field. The <see cref="Extras"/> property captures all remaining
/// fields for flexible downstream processing.
/// </summary>
/// <param name="Id">The unique part identifier.</param>
/// <param name="SessionId">The ID of the session this part belongs to.</param>
/// <param name="MessageId">The ID of the message this part belongs to.</param>
/// <param name="Type">The part type discriminator (e.g. <c>text</c>, <c>tool</c>, <c>file</c>).</param>
/// <param name="Text">The text content for <c>type: "text"</c> parts, or <c>null</c> for other types.</param>
public sealed record PartDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("sessionID")] string SessionId,
    [property: JsonPropertyName("messageID")] string MessageId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string? Text = null
)
{
    /// <summary>Any additional JSON properties not mapped to named fields.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extras { get; init; }

    /// <summary>The raw state JSON element for <c>type: "tool"</c> parts. Contains status, input, output, error, and timing fields.</summary>
    [JsonPropertyName("state")]
    public JsonElement? State { get; init; }

    /// <summary>The call identifier for <c>type: "tool"</c> parts.</summary>
    [JsonPropertyName("callID")]
    public string? CallId { get; init; }

    /// <summary>The tool name for <c>type: "tool"</c> parts.</summary>
    [JsonPropertyName("tool")]
    public string? ToolName { get; init; }
}

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
    [property: JsonPropertyName("parts")] IReadOnlyList<PartDto>? Parts = null
);
