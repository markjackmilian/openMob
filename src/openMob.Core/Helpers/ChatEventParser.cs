using System.Diagnostics;
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
        // The opencode server wraps all SSE events in an envelope:
        //   { "directory": "...", "payload": { "type": "<event-type>", "properties": { ... } } }
        // The SSE wire-level "event:" field is always absent (the server never sets it),
        // so dto.EventType is always "unknown". The real event type is in data.payload.type
        // and the event data is in data.payload.properties.
        if (dto.Data is not { } envelope)
        {
#if DEBUG
            Debug.WriteLine("[SSE_PARSER] dto.Data is null — returning UnknownEvent");
#endif
            return MakeUnknown(dto);
        }

#if DEBUG
        Debug.WriteLine($"[SSE_PARSER] raw envelope JSON: {envelope}");
#endif

        // Extract the project directory from the SSE envelope (REQ-001, REQ-007).
        // The field may be absent in older server versions — fall back to null.
        string? projectDirectory = null;
        if (envelope.TryGetProperty("directory", out var dirEl) &&
            dirEl.ValueKind == JsonValueKind.String)
        {
            projectDirectory = dirEl.GetString();
        }

        if (!envelope.TryGetProperty("payload", out var payloadEl) ||
            payloadEl.ValueKind != JsonValueKind.Object)
        {
#if DEBUG
            Debug.WriteLine($"[SSE_PARSER] no 'payload' object found in envelope — top-level keys: {string.Join(", ", envelope.EnumerateObject().Select(p => p.Name))}");
#endif
            return MakeUnknown(dto);
        }

        if (!payloadEl.TryGetProperty("type", out var typeEl) ||
            typeEl.ValueKind != JsonValueKind.String)
        {
#if DEBUG
            Debug.WriteLine($"[SSE_PARSER] no 'type' string in payload — payload keys: {string.Join(", ", payloadEl.EnumerateObject().Select(p => p.Name))}");
#endif
            return MakeUnknown(dto);
        }

        var eventType = typeEl.GetString() ?? string.Empty;

        // Extract properties element (may be absent for events with no payload)
        payloadEl.TryGetProperty("properties", out var propertiesEl);

#if DEBUG
        Debug.WriteLine($"[SSE_PARSER] eventType='{eventType}' hasProperties={propertiesEl.ValueKind != JsonValueKind.Undefined}");
#endif

        // Build a synthetic DTO with the unwrapped type and properties as Data
        var unwrapped = new OpencodeEventDto(
            EventType: eventType,
            EventId: dto.EventId,
            Data: propertiesEl.ValueKind == JsonValueKind.Undefined ? null : propertiesEl);

        var result = eventType switch
        {
            "server.connected" => new ServerConnectedEvent
            {
                RawEventId = unwrapped.EventId,
                ProjectDirectory = projectDirectory,
            },

            "message.updated" => ParseMessageUpdated(unwrapped, projectDirectory),
            "message.part.updated" => ParseMessagePartUpdated(unwrapped, projectDirectory),
            "message.part.delta" => ParseMessagePartDelta(unwrapped, projectDirectory),
            "session.updated" => ParseSessionUpdated(unwrapped, projectDirectory),
            "session.error" => ParseSessionError(unwrapped, projectDirectory),
            "permission.asked" => ParsePermissionRequested(unwrapped, projectDirectory),
            "permission.requested" => ParsePermissionRequested(unwrapped, projectDirectory),
            "permission.updated" => ParsePermissionUpdated(unwrapped, projectDirectory),

            _ => new UnknownEvent
            {
                RawEventId = unwrapped.EventId,
                RawType = eventType,
                // Preserve the original envelope (not the unwrapped properties) for diagnostics.
                RawData = dto.Data,
                ProjectDirectory = projectDirectory,
            },
        };

#if DEBUG
        Debug.WriteLine($"[SSE_PARSER] parsed as {result.GetType().Name} (Type={result.Type})");
#endif

        return result;
    }

    private static ChatEvent ParseMessageUpdated(OpencodeEventDto dto, string? projectDirectory)
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
                ProjectDirectory = projectDirectory,
            };
        }
        catch
        {
            return MakeUnknown(dto);
        }
    }

    private static ChatEvent ParseMessagePartUpdated(OpencodeEventDto dto, string? projectDirectory)
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
                    ProjectDirectory = projectDirectory,
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
                ProjectDirectory = projectDirectory,
            };
        }
        catch
        {
            return MakeUnknown(dto);
        }
    }

    private static ChatEvent ParseMessagePartDelta(OpencodeEventDto dto, string? projectDirectory)
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
                ProjectDirectory = projectDirectory,
            };
        }
        catch
        {
            return MakeUnknown(dto);
        }
    }

    private static ChatEvent ParseSessionUpdated(OpencodeEventDto dto, string? projectDirectory)
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
                    ProjectDirectory = projectDirectory,
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
                ProjectDirectory = projectDirectory,
            };
        }
        catch
        {
            return MakeUnknown(dto);
        }
    }

    private static ChatEvent ParseSessionError(OpencodeEventDto dto, string? projectDirectory)
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
                ProjectDirectory = projectDirectory,
            };
        }
        catch
        {
            return MakeUnknown(dto);
        }
    }

    private static ChatEvent ParsePermissionRequested(OpencodeEventDto dto, string? projectDirectory)
    {
        if (dto.Data is not { } data)
            return MakeUnknown(dto);

        try
        {
            var id = ReadString(data, "id") ?? ReadString(data, "permissionID");
            var sessionId = ReadString(data, "sessionID");
            var permission = ReadString(data, "permission");
            var patterns = ReadStringArray(data, "patterns") ?? Array.Empty<string>();
            var always = ReadStringArray(data, "always") ?? Array.Empty<string>();

            if (id is null || sessionId is null)
                return MakeUnknown(dto);

            permission ??= id;

            var metadata = ReadDictionary(data, "metadata") ?? new Dictionary<string, object>();

            PermissionRequestedTool? tool = null;
            if (data.TryGetProperty("tool", out var toolEl) && toolEl.ValueKind == JsonValueKind.Object)
            {
                var messageId = ReadString(toolEl, "messageId");
                var callId = ReadString(toolEl, "callId");

                if (messageId is not null && callId is not null)
                {
                    tool = new PermissionRequestedTool
                    {
                        MessageId = messageId,
                        CallId = callId,
                    };
                }
            }

            return new PermissionRequestedEvent
            {
                RawEventId = dto.EventId,
                Id = id,
                SessionId = sessionId,
                Permission = permission,
                Patterns = patterns,
                Metadata = metadata,
                Always = always,
                Tool = tool,
                ProjectDirectory = projectDirectory,
            };
        }
        catch
        {
            return MakeUnknown(dto);
        }
    }

    private static ChatEvent ParsePermissionUpdated(OpencodeEventDto dto, string? projectDirectory)
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
                ProjectDirectory = projectDirectory,
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

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }

    private static IReadOnlyList<string>? ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Array)
            return null;

        var values = new List<string>();
        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                return null;

            var value = item.GetString();
            if (value is null)
                return null;

            values.Add(value);
        }

        return values;
    }

    private static Dictionary<string, object>? ReadDictionary(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Object)
            return null;

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(prop);
        }
        catch
        {
            return null;
        }
    }
}
