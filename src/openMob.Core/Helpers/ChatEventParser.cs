using System.Text.Json;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Models;

namespace openMob.Core.Helpers;

/// <summary>
/// Parses raw <see cref="OpencodeEventDto"/> instances into typed <see cref="ChatEvent"/> objects.
/// Never propagates exceptions — malformed or unrecognised events are returned as <see cref="UnknownEvent"/>.
/// </summary>
internal sealed class ChatEventParser
{
    /// <summary>
    /// Parses a raw SSE event DTO into a typed <see cref="ChatEvent"/>.
    /// </summary>
    /// <param name="dto">The raw event DTO received from the SSE stream.</param>
    /// <returns>
    /// A typed <see cref="ChatEvent"/> matching the event type, or an <see cref="UnknownEvent"/>
    /// if the type is unrecognised or deserialization fails.
    /// </returns>
    internal static ChatEvent Parse(OpencodeEventDto dto)
    {
        // The opencode server wraps all events in an envelope:
        //   { "directory": "...", "payload": { "type": "<event-type>", "properties": { ... } } }
        // The SSE "event:" field is always absent (or "unknown"), so we must read the real
        // event type from data.payload.type and the payload from data.payload.properties.
        if (dto.Data is not { } envelope)
            return MakeUnknown(dto);

        if (!envelope.TryGetProperty("payload", out var payloadEl) ||
            payloadEl.ValueKind != JsonValueKind.Object)
            return MakeUnknown(dto);

        if (!payloadEl.TryGetProperty("type", out var typeEl) ||
            typeEl.ValueKind != JsonValueKind.String)
            return MakeUnknown(dto);

        var eventType = typeEl.GetString() ?? string.Empty;

        // Extract properties element (may be absent for events with no payload)
        payloadEl.TryGetProperty("properties", out var propertiesEl);

        // Build a synthetic DTO with the unwrapped type and properties as Data
        var unwrapped = new OpencodeEventDto(
            EventType: eventType,
            EventId: dto.EventId,
            Data: propertiesEl.ValueKind == JsonValueKind.Undefined ? null : propertiesEl);

        return eventType switch
        {
            "server.connected" => new ServerConnectedEvent
            {
                RawEventId = unwrapped.EventId,
            },

            "message.updated" => ParseMessageUpdated(unwrapped),
            "message.part.updated" => ParseMessagePartUpdated(unwrapped),
            "message.part.delta" => ParseMessagePartDelta(unwrapped),
            "session.updated" => ParseSessionUpdated(unwrapped),
            "session.error" => ParseSessionError(unwrapped),
            "permission.requested" => ParsePermissionRequested(unwrapped),
            "permission.updated" => ParsePermissionUpdated(unwrapped),

            _ => new UnknownEvent
            {
                RawEventId = dto.EventId,
                RawType = eventType,
                RawData = dto.Data,
            },
        };
    }

    private static ChatEvent ParseMessageUpdated(OpencodeEventDto dto)
    {
        if (dto.Data is not { } data)
            return MakeUnknown(dto);

        try
        {
            // Server sends: { "info": {...} } — parts may be absent in intermediate events.
            // We deserialise MessageWithPartsDto directly; if "parts" is missing it defaults
            // to an empty list via the DTO's default value.
            MessageWithPartsDto? message;

            if (data.TryGetProperty("info", out _))
            {
                // properties already has the right shape: { "info": {...}, "parts": [...] }
                message = JsonSerializer.Deserialize<MessageWithPartsDto>(data);
            }
            else
            {
                return MakeUnknown(dto);
            }

            if (message is null)
                return MakeUnknown(dto);

            return new MessageUpdatedEvent
            {
                RawEventId = dto.EventId,
                Message = message,
            };
        }
        catch
        {
            return MakeUnknown(dto);
        }
    }

    private static ChatEvent ParseMessagePartUpdated(OpencodeEventDto dto)
    {
        if (dto.Data is not { } data)
            return MakeUnknown(dto);

        try
        {
            // Server sends: { "part": { <PartDto fields> } }
            JsonElement partEl;
            if (data.TryGetProperty("part", out partEl))
            {
                var part = JsonSerializer.Deserialize<PartDto>(partEl);
                if (part is null)
                    return MakeUnknown(dto);

                return new MessagePartUpdatedEvent
                {
                    RawEventId = dto.EventId,
                    Part = part,
                };
            }

            // Fallback: try deserialising directly (old format or test data)
            var directPart = JsonSerializer.Deserialize<PartDto>(data);
            if (directPart is null)
                return MakeUnknown(dto);

            return new MessagePartUpdatedEvent
            {
                RawEventId = dto.EventId,
                Part = directPart,
            };
        }
        catch
        {
            return MakeUnknown(dto);
        }
    }

    private static ChatEvent ParseMessagePartDelta(OpencodeEventDto dto)
    {
        if (dto.Data is not { } data)
            return MakeUnknown(dto);

        try
        {
            // Server sends: { "sessionID": "...", "messageID": "...", "partID": "...", "field": "text", "delta": "..." }
            var sessionId = data.TryGetProperty("sessionID", out var sidProp) ? sidProp.GetString() : null;
            var messageId = data.TryGetProperty("messageID", out var midProp) ? midProp.GetString() : null;
            var partId = data.TryGetProperty("partID", out var pidProp) ? pidProp.GetString() : null;
            var field = data.TryGetProperty("field", out var fieldProp) ? fieldProp.GetString() : null;
            var delta = data.TryGetProperty("delta", out var deltaProp) ? deltaProp.GetString() : null;

            if (sessionId is null || messageId is null || partId is null || field is null || delta is null)
                return MakeUnknown(dto);

            return new MessagePartDeltaEvent
            {
                RawEventId = dto.EventId,
                SessionId = sessionId,
                MessageId = messageId,
                PartId = partId,
                Field = field,
                Delta = delta,
            };
        }
        catch
        {
            return MakeUnknown(dto);
        }
    }

    private static ChatEvent ParseSessionUpdated(OpencodeEventDto dto)
    {
        if (dto.Data is not { } data)
            return MakeUnknown(dto);

        try
        {
            // Server sends: { "info": { <SessionDto fields> } }
            JsonElement infoEl;
            if (data.TryGetProperty("info", out infoEl))
            {
                var session = JsonSerializer.Deserialize<SessionDto>(infoEl);
                if (session is null)
                    return MakeUnknown(dto);

                return new SessionUpdatedEvent
                {
                    RawEventId = dto.EventId,
                    Session = session,
                };
            }

            // Fallback: try deserialising directly (old format or test data)
            var directSession = JsonSerializer.Deserialize<SessionDto>(data);
            if (directSession is null)
                return MakeUnknown(dto);

            return new SessionUpdatedEvent
            {
                RawEventId = dto.EventId,
                Session = directSession,
            };
        }
        catch
        {
            return MakeUnknown(dto);
        }
    }

    private static ChatEvent ParseSessionError(OpencodeEventDto dto)
    {
        if (dto.Data is not { } data)
            return MakeUnknown(dto);

        try
        {
            // Wire format: { "sessionID": string, "error": string }
            var sessionId = data.TryGetProperty("sessionID", out var sidProp)
                ? sidProp.GetString()
                : null;
            var errorMessage = data.TryGetProperty("error", out var errProp)
                ? errProp.GetString()
                : null;

            if (sessionId is null || errorMessage is null)
                return MakeUnknown(dto);

            return new SessionErrorEvent
            {
                RawEventId = dto.EventId,
                SessionId = sessionId,
                ErrorMessage = errorMessage,
            };
        }
        catch
        {
            return MakeUnknown(dto);
        }
    }

    private static ChatEvent ParsePermissionRequested(OpencodeEventDto dto)
    {
        if (dto.Data is not { } data)
            return MakeUnknown(dto);

        try
        {
            // Wire format: { "sessionID": string, "permissionID": string, ...rest }
            var sessionId = data.TryGetProperty("sessionID", out var sidProp)
                ? sidProp.GetString()
                : null;
            var permissionId = data.TryGetProperty("permissionID", out var pidProp)
                ? pidProp.GetString()
                : null;

            if (sessionId is null || permissionId is null)
                return MakeUnknown(dto);

            return new PermissionRequestedEvent
            {
                RawEventId = dto.EventId,
                SessionId = sessionId,
                PermissionId = permissionId,
                RawPayload = data,
            };
        }
        catch
        {
            return MakeUnknown(dto);
        }
    }

    private static ChatEvent ParsePermissionUpdated(OpencodeEventDto dto)
    {
        if (dto.Data is not { } data)
            return MakeUnknown(dto);

        try
        {
            // Wire format: { "sessionID": string, "permissionID": string, ...rest }
            var sessionId = data.TryGetProperty("sessionID", out var sidProp)
                ? sidProp.GetString()
                : null;
            var permissionId = data.TryGetProperty("permissionID", out var pidProp)
                ? pidProp.GetString()
                : null;

            if (sessionId is null || permissionId is null)
                return MakeUnknown(dto);

            return new PermissionUpdatedEvent
            {
                RawEventId = dto.EventId,
                SessionId = sessionId,
                PermissionId = permissionId,
                RawPayload = data,
            };
        }
        catch
        {
            return MakeUnknown(dto);
        }
    }

    private static UnknownEvent MakeUnknown(OpencodeEventDto dto) =>
        new()
        {
            RawEventId = dto.EventId,
            RawType = dto.EventType,
            RawData = dto.Data,
        };
}
