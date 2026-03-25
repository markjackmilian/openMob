using System.Text.Json;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;

namespace openMob.Core.Models;

/// <summary>
/// Abstract base record for all typed SSE chat events received from the opencode server.
/// Use pattern matching on the concrete derived type to access event-specific data.
/// </summary>
public abstract record ChatEvent
{
    /// <summary>Gets the discriminated event type.</summary>
    public abstract ChatEventType Type { get; }

    /// <summary>
    /// Gets the raw SSE event ID as sent by the server, or <c>null</c> if the server
    /// did not include an <c>id:</c> field. Used for <c>Last-Event-ID</c> reconnect.
    /// </summary>
    public string? RawEventId { get; init; }

    /// <summary>
    /// Gets the absolute path of the project directory extracted from the SSE envelope's
    /// <c>directory</c> field, or <c>null</c> if the field was absent or empty.
    /// Used by consumers to filter events by project context.
    /// </summary>
    public string? ProjectDirectory { get; init; }
}

/// <summary>
/// Raised when the SSE connection is first established.
/// The server always sends this as the first event after a successful connection.
/// </summary>
public sealed record ServerConnectedEvent : ChatEvent
{
    /// <inheritdoc />
    public override ChatEventType Type => ChatEventType.ServerConnected;
}

/// <summary>
/// Raised when a message is created or updated (e.g. streaming text tokens or completion).
/// </summary>
public sealed record MessageUpdatedEvent : ChatEvent
{
    /// <inheritdoc />
    public override ChatEventType Type => ChatEventType.MessageUpdated;

    /// <summary>Gets the updated message with all its parts.</summary>
    public required MessageWithPartsDto Message { get; init; }
}

/// <summary>
/// Raised when a single part of a message is updated.
/// </summary>
public sealed record MessagePartUpdatedEvent : ChatEvent
{
    /// <inheritdoc />
    public override ChatEventType Type => ChatEventType.MessagePartUpdated;

    /// <summary>Gets the updated message part.</summary>
    public required PartDto Part { get; init; }
}

/// <summary>
/// Raised when session metadata is updated.
/// </summary>
public sealed record SessionUpdatedEvent : ChatEvent
{
    /// <inheritdoc />
    public override ChatEventType Type => ChatEventType.SessionUpdated;

    /// <summary>Gets the updated session data.</summary>
    public required SessionDto Session { get; init; }
}

/// <summary>
/// Raised when an error occurs during session processing.
/// </summary>
public sealed record SessionErrorEvent : ChatEvent
{
    /// <inheritdoc />
    public override ChatEventType Type => ChatEventType.SessionError;

    /// <summary>Gets the ID of the session that encountered an error.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the human-readable error message from the server.</summary>
    public required string ErrorMessage { get; init; }
}

/// <summary>
/// Raised when the AI requests a permission (tool call approval).
/// </summary>
public sealed record PermissionRequestedEvent : ChatEvent
{
    /// <inheritdoc />
    public override ChatEventType Type => ChatEventType.PermissionRequested;

    /// <summary>Gets the unique identifier for this permission request.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the ID of the session requesting the permission.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the permission type requested by the server.</summary>
    public required string Permission { get; init; }

    /// <summary>Gets the requested pattern values.</summary>
    public required IReadOnlyList<string> Patterns { get; init; }

    /// <summary>Gets the metadata bag attached to the permission request.</summary>
    public required Dictionary<string, object> Metadata { get; init; }

    /// <summary>Gets the list of always-allow rules supplied by the server.</summary>
    public required IReadOnlyList<string> Always { get; init; }

    /// <summary>Gets the optional tool context for the permission request.</summary>
    public PermissionRequestedTool? Tool { get; init; }

    /// <summary>Gets the legacy alias for <see cref="Id"/>.</summary>
    public string PermissionId => Id;
}

/// <summary>
/// Describes the tool context for a permission request.
/// </summary>
public sealed record PermissionRequestedTool
{
    /// <summary>Gets the message ID associated with the tool call.</summary>
    public required string MessageId { get; init; }

    /// <summary>Gets the call ID associated with the tool call.</summary>
    public required string CallId { get; init; }
}

/// <summary>
/// Raised when the state of a pending permission is updated.
/// </summary>
public sealed record PermissionUpdatedEvent : ChatEvent
{
    /// <inheritdoc />
    public override ChatEventType Type => ChatEventType.PermissionUpdated;

    /// <summary>Gets the ID of the session whose permission was updated.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the unique identifier for the permission that was updated.</summary>
    public required string PermissionId { get; init; }

    /// <summary>Gets the full raw JSON payload for this permission update.</summary>
    public required JsonElement RawPayload { get; init; }
}

/// <summary>
/// Raised when an incremental text delta arrives for a message part during streaming.
/// This is the primary real-time streaming event — each delta contains a small chunk of text.
/// </summary>
public sealed record MessagePartDeltaEvent : ChatEvent
{
    /// <inheritdoc />
    public override ChatEventType Type => ChatEventType.MessagePartDelta;

    /// <summary>Gets the session ID this delta belongs to.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the message ID this delta belongs to.</summary>
    public required string MessageId { get; init; }

    /// <summary>Gets the part ID this delta updates.</summary>
    public required string PartId { get; init; }

    /// <summary>Gets the field being updated (typically <c>"text"</c>).</summary>
    public required string Field { get; init; }

    /// <summary>Gets the incremental text chunk.</summary>
    public required string Delta { get; init; }
}

/// <summary>
/// Represents an unrecognised SSE event type. Raw data is preserved for diagnostics.
/// </summary>
public sealed record UnknownEvent : ChatEvent
{
    /// <inheritdoc />
    public override ChatEventType Type => ChatEventType.Unknown;

    /// <summary>Gets the raw event type string as received from the server.</summary>
    public required string RawType { get; init; }

    /// <summary>Gets the raw JSON data payload, or <c>null</c> if no data was present.</summary>
    public JsonElement? RawData { get; init; }
}
