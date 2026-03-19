using System.Text.Json;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Models;

namespace openMob.Tests.Models;

/// <summary>
/// Unit tests for <see cref="ChatMessage"/> factory methods:
/// <see cref="ChatMessage.FromDto"/> and <see cref="ChatMessage.CreateOptimistic"/>.
/// </summary>
public sealed class ChatMessageTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="MessageWithPartsDto"/> for test scenarios.
    /// </summary>
    private static MessageWithPartsDto BuildMessageDto(
        string id = "msg-1",
        string sessionId = "sess-1",
        string role = "user",
        string text = "Hello",
        bool completed = false)
    {
        var timeObj = completed
            ? new { created = 1710576000000L, completed = 1710576030000L }
            : (object)new { created = 1710576000000L };
        var timeJson = JsonSerializer.SerializeToElement(timeObj);

        var info = new MessageInfoDto(Id: id, SessionId: sessionId, Role: role, Time: timeJson);
        var part = new PartDto(Id: $"part-{id}", SessionId: sessionId, MessageId: id, Type: "text", Text: text);
        return new MessageWithPartsDto(Info: info, Parts: new[] { part });
    }

    /// <summary>
    /// Builds a <see cref="MessageWithPartsDto"/> with no text parts (e.g. tool-only message).
    /// </summary>
    private static MessageWithPartsDto BuildMessageDtoWithNoParts(
        string id = "msg-1",
        string sessionId = "sess-1",
        string role = "assistant")
    {
        var timeJson = JsonSerializer.SerializeToElement(new { created = 1710576000000L, completed = 1710576030000L });

        var info = new MessageInfoDto(Id: id, SessionId: sessionId, Role: role, Time: timeJson);
        var part = new PartDto(Id: $"part-{id}", SessionId: sessionId, MessageId: id, Type: "tool");
        return new MessageWithPartsDto(Info: info, Parts: new[] { part });
    }

    // ─── FromDto — Role mapping ──────────────────────────────────────────────

    [Fact]
    public void FromDto_WithUserRole_SetsIsFromUserTrue()
    {
        // Arrange
        var dto = BuildMessageDto(role: "user");

        // Act
        var result = ChatMessage.FromDto(dto);

        // Assert
        result.IsFromUser.Should().BeTrue();
    }

    [Fact]
    public void FromDto_WithAssistantRole_SetsIsFromUserFalse()
    {
        // Arrange
        var dto = BuildMessageDto(role: "assistant");

        // Act
        var result = ChatMessage.FromDto(dto);

        // Assert
        result.IsFromUser.Should().BeFalse();
    }

    // ─── FromDto — Text content extraction ───────────────────────────────────

    [Fact]
    public void FromDto_WithTextParts_ConcatenatesTextContent()
    {
        // Arrange
        var timeJson = JsonSerializer.SerializeToElement(new { created = 1710576000000L });

        var info = new MessageInfoDto(Id: "msg-1", SessionId: "sess-1", Role: "user", Time: timeJson);
        var parts = new[]
        {
            new PartDto(Id: "p1", SessionId: "sess-1", MessageId: "msg-1", Type: "text", Text: "Hello "),
            new PartDto(Id: "p2", SessionId: "sess-1", MessageId: "msg-1", Type: "text", Text: "World"),
        };
        var dto = new MessageWithPartsDto(Info: info, Parts: parts);

        // Act
        var result = ChatMessage.FromDto(dto);

        // Assert
        result.TextContent.Should().Be("Hello World");
    }

    [Fact]
    public void FromDto_WithNoTextParts_SetsEmptyTextContent()
    {
        // Arrange
        var dto = BuildMessageDtoWithNoParts(role: "assistant");

        // Act
        var result = ChatMessage.FromDto(dto);

        // Assert
        result.TextContent.Should().BeEmpty();
    }

    // ─── FromDto — Streaming state ───────────────────────────────────────────

    [Fact]
    public void FromDto_WithCompletedTimestamp_SetsIsStreamingFalse()
    {
        // Arrange
        var dto = BuildMessageDto(role: "assistant", completed: true);

        // Act
        var result = ChatMessage.FromDto(dto);

        // Assert
        result.IsStreaming.Should().BeFalse();
    }

    [Fact]
    public void FromDto_WithoutCompletedTimestamp_SetsIsStreamingTrue()
    {
        // Arrange
        var dto = BuildMessageDto(role: "assistant", completed: false);

        // Act
        var result = ChatMessage.FromDto(dto);

        // Assert
        result.IsStreaming.Should().BeTrue();
    }

    [Fact]
    public void FromDto_UserMessage_IsStreamingAlwaysFalse()
    {
        // Arrange — user message without completed timestamp
        var dto = BuildMessageDto(role: "user", completed: false);

        // Act
        var result = ChatMessage.FromDto(dto);

        // Assert
        result.IsStreaming.Should().BeFalse();
    }

    // ─── FromDto — Delivery status ───────────────────────────────────────────

    [Fact]
    public void FromDto_SetsDeliveryStatusToSent()
    {
        // Arrange
        var dto = BuildMessageDto();

        // Act
        var result = ChatMessage.FromDto(dto);

        // Assert
        result.DeliveryStatus.Should().Be(MessageDeliveryStatus.Sent);
    }

    // ─── FromDto — Timestamp extraction ──────────────────────────────────────

    [Fact]
    public void FromDto_ExtractsTimestampFromCreatedField()
    {
        // Arrange
        var dto = BuildMessageDto(); // created = 1710576000000L

        // Act
        var result = ChatMessage.FromDto(dto);

        // Assert
        var expected = DateTimeOffset.FromUnixTimeMilliseconds(1710576000000L);
        result.Timestamp.Should().Be(expected);
    }

    // ─── CreateOptimistic ────────────────────────────────────────────────────

    [Fact]
    public void CreateOptimistic_SetsCorrectProperties()
    {
        // Act
        var result = ChatMessage.CreateOptimistic("sess-1", "Hello world");

        // Assert
        result.SessionId.Should().Be("sess-1");
        result.TextContent.Should().Be("Hello world");
        result.IsFromUser.Should().BeTrue();
        result.DeliveryStatus.Should().Be(MessageDeliveryStatus.Sending);
        result.IsStreaming.Should().BeFalse();
        result.Id.Should().NotBeNullOrEmpty();
        result.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ─── Null / edge-case guards [M-003] ─────────────────────────────────────

    [Fact]
    public void FromDto_WithNullDto_ThrowsArgumentNullException()
    {
        // Act
        var act = () => ChatMessage.FromDto(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateOptimistic_WithNullSessionId_ThrowsArgumentNullException()
    {
        // Act
        var act = () => ChatMessage.CreateOptimistic(null!, "Hello");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateOptimistic_WithNullText_ThrowsArgumentNullException()
    {
        // Act
        var act = () => ChatMessage.CreateOptimistic("sess-1", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ExtractTextContent_WithNullParts_ReturnsEmpty()
    {
        // Act
        var result = ChatMessage.ExtractTextContent(null!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractTextContent_WithEmptyParts_ReturnsEmpty()
    {
        // Act
        var result = ChatMessage.ExtractTextContent(Array.Empty<PartDto>());

        // Assert
        result.Should().BeEmpty();
    }

    // ─── FromDto — SenderType mapping [Chat Page Redesign] ───────────────────

    [Fact]
    public void FromDto_WhenRoleIsUser_SetsSenderTypeToUser()
    {
        // Arrange
        var dto = BuildMessageDto(role: "user");

        // Act
        var result = ChatMessage.FromDto(dto);

        // Assert
        result.SenderType.Should().Be(SenderType.User);
    }

    [Fact]
    public void FromDto_WhenRoleIsAssistant_SetsSenderTypeToAgent()
    {
        // Arrange
        var dto = BuildMessageDto(role: "assistant");

        // Act
        var result = ChatMessage.FromDto(dto);

        // Assert
        result.SenderType.Should().Be(SenderType.Agent);
    }

    [Fact]
    public void FromDto_WhenRoleIsUser_SetsSenderNameToYou()
    {
        // Arrange
        var dto = BuildMessageDto(role: "user");

        // Act
        var result = ChatMessage.FromDto(dto);

        // Assert
        result.SenderName.Should().Be("You");
    }

    [Fact]
    public void FromDto_WhenRoleIsAssistant_SetsSenderNameToAssistant()
    {
        // Arrange
        var dto = BuildMessageDto(role: "assistant");

        // Act
        var result = ChatMessage.FromDto(dto);

        // Assert
        result.SenderName.Should().Be("Assistant");
    }

    // ─── CreateOptimistic — SenderType [Chat Page Redesign] ──────────────────

    [Fact]
    public void CreateOptimistic_SetsSenderTypeToUser()
    {
        // Act
        var result = ChatMessage.CreateOptimistic("sess-1", "Hello");

        // Assert
        result.SenderType.Should().Be(SenderType.User);
    }

    [Fact]
    public void CreateOptimistic_SetsSenderNameToYou()
    {
        // Act
        var result = ChatMessage.CreateOptimistic("sess-1", "Hello");

        // Assert
        result.SenderName.Should().Be("You");
    }
}
