using System.Text.Json;
using openMob.Core.Helpers;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Models;

namespace openMob.Tests.Helpers;

/// <summary>
/// Unit tests for <see cref="ChatEventParser"/>.
/// Verifies that raw <see cref="OpencodeEventDto"/> instances are correctly mapped
/// to typed <see cref="ChatEvent"/> objects, and that malformed or unknown events
/// are safely returned as <see cref="UnknownEvent"/> without throwing.
/// </summary>
public sealed class ChatEventParserTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    // ─── Envelope builder ─────────────────────────────────────────────────────

    /// <summary>
    /// Wraps a payload in the opencode SSE envelope:
    /// <c>{ "payload": { "type": "&lt;type&gt;", "properties": &lt;properties&gt; } }</c>.
    /// </summary>
    private static JsonElement BuildEnvelope(string type, object? properties = null)
    {
        return JsonSerializer.SerializeToElement(new
        {
            payload = new
            {
                type,
                properties = properties ?? new { }
            }
        });
    }

    /// <summary>Builds a minimal valid <see cref="MessageWithPartsDto"/> properties object.</summary>
    private static object BuildMessageWithPartsProperties(
        string id = "msg-1",
        string sessionId = "sess-1",
        string role = "user")
    {
        return new
        {
            info = new
            {
                id,
                sessionID = sessionId,
                role,
                time = new { created = 0L }
            },
            parts = Array.Empty<object>()
        };
    }

    /// <summary>Builds a minimal valid <see cref="PartDto"/> properties object.</summary>
    private static object BuildPartProperties(
        string id = "part-1",
        string sessionId = "sess-1",
        string messageId = "msg-1",
        string type = "text",
        string? text = null)
    {
        return new
        {
            part = new
            {
                id,
                sessionID = sessionId,
                messageID = messageId,
                type,
                text,
            }
        };
    }

    /// <summary>Builds a minimal valid <see cref="SessionDto"/> properties object.</summary>
    private static object BuildSessionProperties(
        string id = "sess-1",
        string projectId = "proj-1")
    {
        return new
        {
            info = new
            {
                id,
                projectID = projectId,
                directory = "/dir",
                parentID = (string?)null,
                summary = (object?)null,
                share = (object?)null,
                title = "Test Session",
                version = "1.0",
                time = new { created = 0L, updated = 0L, compacting = (long?)null },
                revert = (object?)null
            }
        };
    }

    // ─── server.connected ─────────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenEventTypeIsServerConnected_ReturnsServerConnectedEvent()
    {
        // Arrange — server sends envelope with payload.type = "server.connected"
        var data = BuildEnvelope("server.connected");
        var dto = new OpencodeEventDto("unknown", null, data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<ServerConnectedEvent>();
        result.Type.Should().Be(ChatEventType.ServerConnected);
    }

    // ─── message.updated ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenEventTypeIsMessageUpdated_WithValidData_ReturnsMessageUpdatedEvent()
    {
        // Arrange
        var data = BuildEnvelope("message.updated", BuildMessageWithPartsProperties());
        var dto = new OpencodeEventDto("unknown", "evt-1", data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<MessageUpdatedEvent>();
        result.As<MessageUpdatedEvent>().Message.Should().NotBeNull();
    }

    [Fact]
    public void Parse_WhenDataIsNull_ReturnsUnknownEvent()
    {
        // Arrange — no envelope at all
        var dto = new OpencodeEventDto("unknown", null, null);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<UnknownEvent>();
    }

    [Fact]
    public void Parse_WhenEnvelopeMissingPayload_ReturnsUnknownEvent()
    {
        // Arrange — envelope without "payload" key
        var data = JsonSerializer.SerializeToElement(new { directory = "/dir" });
        var dto = new OpencodeEventDto("unknown", null, data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<UnknownEvent>();
    }

    // ─── message.part.updated ─────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenEventTypeIsMessagePartUpdated_WithValidData_ReturnsMessagePartUpdatedEvent()
    {
        // Arrange
        var data = BuildEnvelope("message.part.updated", BuildPartProperties());
        var dto = new OpencodeEventDto("unknown", "evt-2", data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<MessagePartUpdatedEvent>();
        result.As<MessagePartUpdatedEvent>().Part.Should().NotBeNull();
    }

    // ─── message.part.delta ───────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenEventTypeIsMessagePartDelta_WithValidData_ReturnsMessagePartDeltaEvent()
    {
        // Arrange
        var data = BuildEnvelope("message.part.delta", new
        {
            sessionID = "sess-1",
            messageID = "msg-1",
            partID = "part-1",
            field = "text",
            delta = "Hello"
        });
        var dto = new OpencodeEventDto("unknown", "evt-delta", data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<MessagePartDeltaEvent>();
        var deltaEvent = result.As<MessagePartDeltaEvent>();
        deltaEvent.SessionId.Should().Be("sess-1");
        deltaEvent.MessageId.Should().Be("msg-1");
        deltaEvent.PartId.Should().Be("part-1");
        deltaEvent.Field.Should().Be("text");
        deltaEvent.Delta.Should().Be("Hello");
    }

    // ─── session.updated ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenEventTypeIsSessionUpdated_WithValidData_ReturnsSessionUpdatedEvent()
    {
        // Arrange
        var data = BuildEnvelope("session.updated", BuildSessionProperties());
        var dto = new OpencodeEventDto("unknown", "evt-3", data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<SessionUpdatedEvent>();
        result.As<SessionUpdatedEvent>().Session.Should().NotBeNull();
    }

    // ─── session.error ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenEventTypeIsSessionError_WithValidData_ReturnsSessionErrorEvent()
    {
        // Arrange
        var data = BuildEnvelope("session.error", new
        {
            sessionID = "sess-42",
            error = "Something went wrong"
        });
        var dto = new OpencodeEventDto("unknown", "evt-4", data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<SessionErrorEvent>();
        var errorEvent = result.As<SessionErrorEvent>();
        errorEvent.SessionId.Should().Be("sess-42");
        errorEvent.ErrorMessage.Should().Be("Something went wrong");
    }

    // ─── permission.requested ─────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenEventTypeIsPermissionRequested_WithValidData_ReturnsPermissionRequestedEvent()
    {
        // Arrange
        var data = BuildEnvelope("permission.requested", new
        {
            sessionID = "sess-1",
            permissionID = "perm-99"
        });
        var dto = new OpencodeEventDto("unknown", "evt-5", data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<PermissionRequestedEvent>();
        var permEvent = result.As<PermissionRequestedEvent>();
        permEvent.SessionId.Should().Be("sess-1");
        permEvent.PermissionId.Should().Be("perm-99");
    }

    // ─── permission.updated ───────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenEventTypeIsPermissionUpdated_WithValidData_ReturnsPermissionUpdatedEvent()
    {
        // Arrange
        var data = BuildEnvelope("permission.updated", new
        {
            sessionID = "sess-1",
            permissionID = "perm-99"
        });
        var dto = new OpencodeEventDto("unknown", "evt-6", data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<PermissionUpdatedEvent>();
        var permEvent = result.As<PermissionUpdatedEvent>();
        permEvent.SessionId.Should().Be("sess-1");
        permEvent.PermissionId.Should().Be("perm-99");
    }

    // ─── unknown event type ───────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenPayloadTypeIsUnknown_ReturnsUnknownEvent()
    {
        // Arrange
        var data = BuildEnvelope("some.unknown.type");
        var dto = new OpencodeEventDto("unknown", null, data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<UnknownEvent>();
        result.As<UnknownEvent>().RawType.Should().Be("some.unknown.type");
    }

    // ─── RawEventId preservation ──────────────────────────────────────────────

    [Fact]
    public void Parse_PreservesRawEventId_ForServerConnectedEvent()
    {
        // Arrange
        var data = BuildEnvelope("server.connected");
        var dto = new OpencodeEventDto("unknown", "evt-123", data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.RawEventId.Should().Be("evt-123");
    }

    [Fact]
    public void Parse_PreservesRawEventId_ForUnknownEvent()
    {
        // Arrange
        var data = BuildEnvelope("some.unknown.type");
        var dto = new OpencodeEventDto("unknown", "evt-123", data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.RawEventId.Should().Be("evt-123");
    }

    // ─── null data guard ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenDataIsNull_ForAnyEvent_ReturnsUnknownEvent()
    {
        // Arrange — no envelope at all
        var dto = new OpencodeEventDto("unknown", null, null);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<UnknownEvent>();
    }

    // ─── session.error missing fields ─────────────────────────────────────────

    [Fact]
    public void Parse_WhenSessionErrorMissingSessionId_ReturnsUnknownEvent()
    {
        // Arrange — only has "error", no "sessionID"
        var data = BuildEnvelope("session.error", new { error = "Something went wrong" });
        var dto = new OpencodeEventDto("unknown", null, data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<UnknownEvent>();
    }

    [Fact]
    public void Parse_WhenSessionErrorMissingErrorMessage_ReturnsUnknownEvent()
    {
        // Arrange — only has "sessionID", no "error"
        var data = BuildEnvelope("session.error", new { sessionID = "sess-1" });
        var dto = new OpencodeEventDto("unknown", null, data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<UnknownEvent>();
    }

    // ─── permission missing fields ────────────────────────────────────────────

    [Theory]
    [InlineData("permission.requested")]
    [InlineData("permission.updated")]
    public void Parse_WhenPermissionEventMissingPermissionId_ReturnsUnknownEvent(string eventType)
    {
        // Arrange — only has "sessionID", no "permissionID"
        var data = BuildEnvelope(eventType, new { sessionID = "sess-1" });
        var dto = new OpencodeEventDto("unknown", null, data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<UnknownEvent>();
    }

    // ─── ProjectDirectory extraction ──────────────────────────────────────────

    /// <summary>
    /// Wraps a payload in the opencode SSE envelope WITH a <c>directory</c> field:
    /// <c>{ "directory": "&lt;dir&gt;", "payload": { "type": "&lt;type&gt;", "properties": &lt;properties&gt; } }</c>.
    /// </summary>
    private static JsonElement BuildEnvelopeWithDirectory(string type, object directory, object? properties = null)
    {
        return JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["directory"] = directory,
            ["payload"] = new
            {
                type,
                properties = properties ?? new { }
            }
        });
    }

    [Fact]
    public void Parse_WhenEnvelopeHasDirectory_SetsProjectDirectoryOnEvent()
    {
        // Arrange
        var data = BuildEnvelopeWithDirectory("server.connected", "/path/to/project");
        var dto = new OpencodeEventDto("unknown", null, data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<ServerConnectedEvent>();
        result.ProjectDirectory.Should().Be("/path/to/project");
    }

    [Fact]
    public void Parse_WhenEnvelopeHasNoDirectory_SetsProjectDirectoryToNull()
    {
        // Arrange — standard envelope without directory field
        var data = BuildEnvelope("server.connected");
        var dto = new OpencodeEventDto("unknown", null, data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<ServerConnectedEvent>();
        result.ProjectDirectory.Should().BeNull();
    }

    [Fact]
    public void Parse_WhenDirectoryIsEmptyString_SetsProjectDirectoryToEmpty()
    {
        // Arrange
        var data = BuildEnvelopeWithDirectory("server.connected", "");
        var dto = new OpencodeEventDto("unknown", null, data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<ServerConnectedEvent>();
        result.ProjectDirectory.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WhenDirectoryIsNotString_SetsProjectDirectoryToNull()
    {
        // Arrange — directory is a number, not a string
        var data = BuildEnvelopeWithDirectory("server.connected", 123);
        var dto = new OpencodeEventDto("unknown", null, data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<ServerConnectedEvent>();
        result.ProjectDirectory.Should().BeNull();
    }
}
