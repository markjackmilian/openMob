using System.Text.Json;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Models;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for the three SSE event handlers in <see cref="ChatViewModel"/>:
/// <list type="bullet">
///   <item><see cref="ChatViewModel"/> HandleMessagePartDelta — appends delta text or creates placeholder</item>
///   <item><see cref="ChatViewModel"/> HandleMessagePartUpdated — sets TextContent for type="text" parts</item>
///   <item><see cref="ChatViewModel"/> HandleMessageUpdated — updates or adds message from full DTO</item>
/// </list>
/// All tests use <see cref="IChatService.SubscribeToEventsAsync"/> to inject SSE events
/// and verify the resulting <see cref="ChatViewModel.Messages"/> state.
/// </summary>
public sealed class ChatViewModelSseTests
{
    private readonly IProjectService _projectService;
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;
    private readonly IOpencodeConnectionManager _connectionManager;
    private readonly IProviderService _providerService;
    private readonly IProjectPreferenceService _preferenceService;
    private readonly IChatService _chatService;
    private readonly IOpencodeApiClient _apiClient;
    private readonly IDispatcherService _dispatcher;
    private readonly ChatViewModel _sut;

    public ChatViewModelSseTests()
    {
        _projectService = Substitute.For<IProjectService>();
        _sessionService = Substitute.For<ISessionService>();
        _navigationService = Substitute.For<INavigationService>();
        _popupService = Substitute.For<IAppPopupService>();
        _connectionManager = Substitute.For<IOpencodeConnectionManager>();
        _providerService = Substitute.For<IProviderService>();
        _preferenceService = Substitute.For<IProjectPreferenceService>();
        _chatService = Substitute.For<IChatService>();
        _apiClient = Substitute.For<IOpencodeApiClient>();
        _dispatcher = Substitute.For<IDispatcherService>();

        // CRITICAL: IDispatcherService mock must execute the action synchronously
        _dispatcher.When(d => d.Dispatch(Arg.Any<Action>())).Do(ci => ci.Arg<Action>()());

        // Default: server connected, provider configured
        _connectionManager.ConnectionStatus.Returns(ServerConnectionStatus.Connected);
        _providerService.HasAnyProviderConfiguredAsync(Arg.Any<CancellationToken>()).Returns(true);

        _sut = new ChatViewModel(
            _projectService,
            _sessionService,
            _navigationService,
            _popupService,
            _connectionManager,
            _providerService,
            _preferenceService,
            _chatService,
            _apiClient,
            _dispatcher);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Produces an <see cref="IAsyncEnumerable{ChatEvent}"/> that yields each event
    /// in order, yielding control between each one.
    /// </summary>
    private static async IAsyncEnumerable<ChatEvent> YieldEvents(params ChatEvent[] events)
    {
        foreach (var e in events)
        {
            yield return e;
            await Task.Yield();
        }
    }

    /// <summary>
    /// Builds a <see cref="MessageWithPartsDto"/> with a single text part.
    /// </summary>
    private static MessageWithPartsDto BuildMessageDto(
        string id = "msg-1",
        string sessionId = "sess-1",
        string role = "assistant",
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
    /// Sets up the ViewModel with session "sess-1", mocks GetMessagesAsync to return
    /// <paramref name="existingMessages"/> (or empty), mocks SubscribeToEventsAsync to
    /// yield <paramref name="events"/>, calls SetSession, and waits for SSE processing.
    /// </summary>
    private async Task TriggerSseEvents(
        ChatEvent[] events,
        List<MessageWithPartsDto>? existingMessages = null)
    {
        var messages = existingMessages ?? new List<MessageWithPartsDto>();

        _chatService
            .GetMessagesAsync("sess-1", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Ok(messages));

        _chatService
            .SubscribeToEventsAsync(Arg.Any<CancellationToken>())
            .Returns(YieldEvents(events));

        _sut.SetSession("sess-1");

        // Allow the background SSE task to process all yielded events
        await Task.Delay(200);
    }

    // ─── HandleMessagePartDelta ───────────────────────────────────────────────

    [Fact]
    public async Task HandleMessagePartDelta_WhenMessageExists_AppendsTextToExistingMessage()
    {
        // Arrange
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: "Hello"),
        };
        var deltaEvent = new MessagePartDeltaEvent
        {
            SessionId = "sess-1",
            MessageId = "msg-1",
            PartId = "part-1",
            Field = "text",
            Delta = " World",
        };

        // Act
        await TriggerSseEvents(new ChatEvent[] { deltaEvent }, existingMessages);

        // Assert
        var msg = _sut.Messages.First(m => m.Id == "msg-1");
        msg.TextContent.Should().Be("Hello World");
        msg.IsStreaming.Should().BeTrue();
    }

    [Fact]
    public async Task HandleMessagePartDelta_WhenMessageNotFound_CreatesPlaceholderMessage()
    {
        // Arrange — Messages is empty; no existing messages
        var deltaEvent = new MessagePartDeltaEvent
        {
            SessionId = "sess-1",
            MessageId = "msg-new",
            PartId = "part-1",
            Field = "text",
            Delta = "Hi",
        };

        // Act
        await TriggerSseEvents(new ChatEvent[] { deltaEvent });

        // Assert
        _sut.Messages.Should().ContainSingle();
        var placeholder = _sut.Messages.First();
        placeholder.Id.Should().Be("msg-new");
        placeholder.TextContent.Should().Be("Hi");
        placeholder.IsFromUser.Should().BeFalse();
        placeholder.IsStreaming.Should().BeTrue();
    }

    [Fact]
    public async Task HandleMessagePartDelta_WhenSessionIdDoesNotMatch_DoesNotUpdateMessages()
    {
        // Arrange
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: "Hello"),
        };
        var deltaEvent = new MessagePartDeltaEvent
        {
            SessionId = "sess-OTHER",
            MessageId = "msg-1",
            PartId = "part-1",
            Field = "text",
            Delta = " World",
        };

        // Act
        await TriggerSseEvents(new ChatEvent[] { deltaEvent }, existingMessages);

        // Assert
        var msg = _sut.Messages.First(m => m.Id == "msg-1");
        msg.TextContent.Should().Be("Hello");
    }

    [Fact]
    public async Task HandleMessagePartDelta_WhenFieldIsNotText_DoesNotUpdateMessages()
    {
        // Arrange
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: "Hello"),
        };
        var deltaEvent = new MessagePartDeltaEvent
        {
            SessionId = "sess-1",
            MessageId = "msg-1",
            PartId = "part-1",
            Field = "reasoning",
            Delta = "thinking...",
        };

        // Act
        await TriggerSseEvents(new ChatEvent[] { deltaEvent }, existingMessages);

        // Assert
        var msg = _sut.Messages.First(m => m.Id == "msg-1");
        msg.TextContent.Should().Be("Hello");
    }

    // ─── HandleMessagePartUpdated ─────────────────────────────────────────────

    [Fact]
    public async Task HandleMessagePartUpdated_WhenTextPartAndMessageExists_UpdatesTextContent()
    {
        // Arrange
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: ""),
        };
        var partDto = new PartDto(
            Id: "part-1",
            SessionId: "sess-1",
            MessageId: "msg-1",
            Type: "text",
            Text: "Full response");
        var partUpdatedEvent = new MessagePartUpdatedEvent { Part = partDto };

        // Act
        await TriggerSseEvents(new ChatEvent[] { partUpdatedEvent }, existingMessages);

        // Assert
        var msg = _sut.Messages.First(m => m.Id == "msg-1");
        msg.TextContent.Should().Be("Full response");
        msg.IsStreaming.Should().BeTrue();
    }

    [Fact]
    public async Task HandleMessagePartUpdated_WhenSessionIdDoesNotMatch_DoesNotUpdateMessages()
    {
        // Arrange
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: ""),
        };
        var partDto = new PartDto(
            Id: "part-1",
            SessionId: "sess-OTHER",
            MessageId: "msg-1",
            Type: "text",
            Text: "Full response");
        var partUpdatedEvent = new MessagePartUpdatedEvent { Part = partDto };

        // Act
        await TriggerSseEvents(new ChatEvent[] { partUpdatedEvent }, existingMessages);

        // Assert
        var msg = _sut.Messages.First(m => m.Id == "msg-1");
        msg.TextContent.Should().Be("");
    }

    [Fact]
    public async Task HandleMessagePartUpdated_WhenPartTypeIsNotText_DoesNotUpdateTextContent()
    {
        // Arrange
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: "existing", completed: true),
        };
        var partDto = new PartDto(
            Id: "part-1",
            SessionId: "sess-1",
            MessageId: "msg-1",
            Type: "tool",
            Text: null);
        var partUpdatedEvent = new MessagePartUpdatedEvent { Part = partDto };

        // Act
        await TriggerSseEvents(new ChatEvent[] { partUpdatedEvent }, existingMessages);

        // Assert
        var msg = _sut.Messages.First(m => m.Id == "msg-1");
        msg.TextContent.Should().Be("existing");
        msg.IsStreaming.Should().BeFalse();
    }

    // ─── HandleMessageUpdated ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleMessageUpdated_WhenMessageExists_UpdatesTextContent()
    {
        // Arrange
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: ""),
        };
        var updatedDto = BuildMessageDto(
            id: "msg-1",
            sessionId: "sess-1",
            role: "assistant",
            text: "Complete answer");
        var messageUpdatedEvent = new MessageUpdatedEvent { Message = updatedDto };

        // Act
        await TriggerSseEvents(new ChatEvent[] { messageUpdatedEvent }, existingMessages);

        // Assert
        var msg = _sut.Messages.First(m => m.Id == "msg-1");
        msg.TextContent.Should().Be("Complete answer");
    }

    [Fact]
    public async Task HandleMessageUpdated_WhenMessageNotFound_AddsNewMessage()
    {
        // Arrange — Messages is empty; no existing messages
        var newDto = BuildMessageDto(
            id: "msg-new",
            sessionId: "sess-1",
            role: "assistant",
            text: "New response");
        var messageUpdatedEvent = new MessageUpdatedEvent { Message = newDto };

        // Act
        await TriggerSseEvents(new ChatEvent[] { messageUpdatedEvent });

        // Assert
        _sut.Messages.Should().ContainSingle(m => m.Id == "msg-new");
    }

    [Fact]
    public async Task HandleMessageUpdated_WhenSessionIdDoesNotMatch_DoesNotUpdateMessages()
    {
        // Arrange
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: "Original"),
        };
        var mismatchedDto = BuildMessageDto(
            id: "msg-1",
            sessionId: "sess-OTHER",
            role: "assistant",
            text: "Should not appear");
        var messageUpdatedEvent = new MessageUpdatedEvent { Message = mismatchedDto };

        // Act
        await TriggerSseEvents(new ChatEvent[] { messageUpdatedEvent }, existingMessages);

        // Assert
        _sut.Messages.Should().HaveCount(1);
        _sut.Messages.First().TextContent.Should().Be("Original");
    }
}
