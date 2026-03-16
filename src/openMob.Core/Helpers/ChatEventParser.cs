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
        return dto.EventType switch
        {
            "server.connected" => new ServerConnectedEvent
            {
                RawEventId = dto.EventId,
            },

            "message.updated" => ParseMessageUpdated(dto),
            "message.part.updated" => ParseMessagePartUpdated(dto),
            "session.updated" => ParseSessionUpdated(dto),
            "session.error" => ParseSessionError(dto),
            "permission.requested" => ParsePermissionRequested(dto),
            "permission.updated" => ParsePermissionUpdated(dto),

            _ => new UnknownEvent
            {
                RawEventId = dto.EventId,
                RawType = dto.EventType,
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
            var message = JsonSerializer.Deserialize<MessageWithPartsDto>(data);
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
            var part = JsonSerializer.Deserialize<PartDto>(data);
            if (part is null)
                return MakeUnknown(dto);

            return new MessagePartUpdatedEvent
            {
                RawEventId = dto.EventId,
                Part = part,
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
            var session = JsonSerializer.Deserialize<SessionDto>(data);
            if (session is null)
                return MakeUnknown(dto);

            return new SessionUpdatedEvent
            {
                RawEventId = dto.EventId,
                Session = session,
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
