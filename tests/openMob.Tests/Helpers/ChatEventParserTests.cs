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

    /// <summary>Builds a minimal valid <see cref="MessageWithPartsDto"/> JSON element.</summary>
    private static JsonElement BuildMessageWithPartsJson(
        string id = "msg-1",
        string sessionId = "sess-1",
        string role = "user")
    {
        return JsonSerializer.SerializeToElement(new
        {
            info = new
            {
                id,
                sessionID = sessionId,
                role,
                time = new { created = 0L }
            },
            parts = Array.Empty<object>()
        });
    }

    /// <summary>Builds a minimal valid <see cref="PartDto"/> JSON element.</summary>
    private static JsonElement BuildPartJson(
        string id = "part-1",
        string sessionId = "sess-1",
        string messageId = "msg-1",
        string type = "text")
    {
        return JsonSerializer.SerializeToElement(new
        {
            id,
            sessionID = sessionId,
            messageID = messageId,
            type,
            payload = new { }
        });
    }

    /// <summary>Builds a minimal valid <see cref="SessionDto"/> JSON element.</summary>
    private static JsonElement BuildSessionJson(
        string id = "sess-1",
        string projectId = "proj-1")
    {
        return JsonSerializer.SerializeToElement(new
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
        });
    }

    // ─── server.connected ─────────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenEventTypeIsServerConnected_ReturnsServerConnectedEvent()
    {
        // Arrange
        var dto = new OpencodeEventDto("server.connected", null, null);

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
        var data = BuildMessageWithPartsJson();
        var dto = new OpencodeEventDto("message.updated", "evt-1", data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<MessageUpdatedEvent>();
        result.As<MessageUpdatedEvent>().Message.Should().NotBeNull();
    }

    [Fact]
    public void Parse_WhenEventTypeIsMessageUpdated_WithInvalidData_ReturnsUnknownEvent()
    {
        // Arrange — malformed JSON that cannot deserialize to MessageWithPartsDto
        var malformedData = JsonSerializer.SerializeToElement("this is not a message object");
        var dto = new OpencodeEventDto("message.updated", "evt-bad", malformedData);

        // Act
        var act = () => ChatEventParser.Parse(dto);

        // Assert — no exception thrown, returns UnknownEvent
        act.Should().NotThrow();
        var result = act();
        result.Should().BeOfType<UnknownEvent>();
    }

    [Fact]
    public void Parse_WhenDataIsNull_ForMessageUpdated_ReturnsUnknownEvent()
    {
        // Arrange
        var dto = new OpencodeEventDto("message.updated", null, null);

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
        var data = BuildPartJson();
        var dto = new OpencodeEventDto("message.part.updated", "evt-2", data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<MessagePartUpdatedEvent>();
        result.As<MessagePartUpdatedEvent>().Part.Should().NotBeNull();
    }

    // ─── session.updated ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_WhenEventTypeIsSessionUpdated_WithValidData_ReturnsSessionUpdatedEvent()
    {
        // Arrange
        var data = BuildSessionJson();
        var dto = new OpencodeEventDto("session.updated", "evt-3", data);

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
        var data = JsonSerializer.SerializeToElement(new
        {
            sessionID = "sess-42",
            error = "Something went wrong"
        });
        var dto = new OpencodeEventDto("session.error", "evt-4", data);

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
        var data = JsonSerializer.SerializeToElement(new
        {
            sessionID = "sess-1",
            permissionID = "perm-99"
        });
        var dto = new OpencodeEventDto("permission.requested", "evt-5", data);

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
        var data = JsonSerializer.SerializeToElement(new
        {
            sessionID = "sess-1",
            permissionID = "perm-99"
        });
        var dto = new OpencodeEventDto("permission.updated", "evt-6", data);

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
    public void Parse_WhenEventTypeIsUnknown_ReturnsUnknownEvent()
    {
        // Arrange
        var dto = new OpencodeEventDto("some.unknown.type", null, null);

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
        var dto = new OpencodeEventDto("server.connected", "evt-123", null);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.RawEventId.Should().Be("evt-123");
    }

    [Fact]
    public void Parse_PreservesRawEventId_ForUnknownEvent()
    {
        // Arrange
        var dto = new OpencodeEventDto("some.unknown.type", "evt-123", null);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.RawEventId.Should().Be("evt-123");
    }

    // ─── null data guard for all data-requiring types ─────────────────────────

    [Theory]
    [InlineData("message.updated")]
    [InlineData("message.part.updated")]
    [InlineData("session.updated")]
    [InlineData("session.error")]
    [InlineData("permission.requested")]
    [InlineData("permission.updated")]
    public void Parse_WhenDataIsNull_ForKnownTypeThatRequiresData_ReturnsUnknownEvent(string eventType)
    {
        // Arrange
        var dto = new OpencodeEventDto(eventType, null, null);

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
        var data = JsonSerializer.SerializeToElement(new { error = "Something went wrong" });
        var dto = new OpencodeEventDto("session.error", null, data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<UnknownEvent>();
    }

    [Fact]
    public void Parse_WhenSessionErrorMissingErrorMessage_ReturnsUnknownEvent()
    {
        // Arrange — only has "sessionID", no "error"
        var data = JsonSerializer.SerializeToElement(new { sessionID = "sess-1" });
        var dto = new OpencodeEventDto("session.error", null, data);

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
        var data = JsonSerializer.SerializeToElement(new { sessionID = "sess-1" });
        var dto = new OpencodeEventDto(eventType, null, data);

        // Act
        var result = ChatEventParser.Parse(dto);

        // Assert
        result.Should().BeOfType<UnknownEvent>();
    }
}
