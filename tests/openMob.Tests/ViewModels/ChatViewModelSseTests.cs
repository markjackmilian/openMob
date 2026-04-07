using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using openMob.Core.Data.Entities;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Messages;
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
[Collection(MessengerTestCollection.Name)]
public sealed class ChatViewModelSseTests : IDisposable
{
    private readonly IProjectService _projectService;
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;
    private readonly IOpencodeConnectionManager _connectionManager;
    private readonly IProjectPreferenceService _preferenceService;
    private readonly IChatService _chatService;
    private readonly IOpencodeApiClient _apiClient;
    private readonly IDispatcherService _dispatcher;
    private readonly IActiveProjectService _activeProjectService;
    private readonly IHeartbeatMonitorService _heartbeatMonitor;
    private readonly ChatViewModel _sut;

    public ChatViewModelSseTests()
    {
        _projectService = Substitute.For<IProjectService>();
        _sessionService = Substitute.For<ISessionService>();
        _navigationService = Substitute.For<INavigationService>();
        _popupService = Substitute.For<IAppPopupService>();
        _connectionManager = Substitute.For<IOpencodeConnectionManager>();
        _preferenceService = Substitute.For<IProjectPreferenceService>();
        _chatService = Substitute.For<IChatService>();
        _apiClient = Substitute.For<IOpencodeApiClient>();
        _dispatcher = Substitute.For<IDispatcherService>();
        _activeProjectService = Substitute.For<IActiveProjectService>();
        _heartbeatMonitor = Substitute.For<IHeartbeatMonitorService>();

        // CRITICAL: IDispatcherService mock must execute the action synchronously
        _dispatcher.When(d => d.Dispatch(Arg.Any<Action>())).Do(ci => ci.Arg<Action>()());

        // Default: server connected
        _connectionManager.ConnectionStatus.Returns(ServerConnectionStatus.Connected);

        _sut = new ChatViewModel(
            _projectService,
            _sessionService,
            _navigationService,
            _popupService,
            _connectionManager,
            _preferenceService,
            _chatService,
            _apiClient,
            _dispatcher,
            _activeProjectService,
            _heartbeatMonitor);
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
    /// Builds a typed permission request event for the SSE stream.
    /// </summary>
    private static PermissionRequestedEvent BuildPermissionRequestedEvent(
        string id = "per-1",
        string sessionId = "sess-1",
        string permission = "bash",
        string[]? patterns = null)
    {
        return new PermissionRequestedEvent
        {
            Id = id,
            SessionId = sessionId,
            Permission = permission,
            Patterns = patterns ?? ["src/**"],
            Metadata = new Dictionary<string, object>(),
            Always = ["src/**"],
        };
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
    public async Task HandleMessagePartDelta_WhenFieldIsReasoning_DoesNotUpdateTextContent()
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

    [Fact]
    public async Task HandleMessagePartDelta_WhenFieldIsUnknown_DoesNotUpdateAnyContent()
    {
        // Arrange
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: "Original text"),
        };
        var deltaEvent = new MessagePartDeltaEvent
        {
            SessionId = "sess-1",
            MessageId = "msg-1",
            PartId = "part-1",
            Field = "some_unknown_field",
            Delta = "should be ignored",
            ProjectDirectory = null,
        };

        // Act
        await TriggerSseEvents(new ChatEvent[] { deltaEvent }, existingMessages);

        // Assert
        var msg = _sut.Messages.First(m => m.Id == "msg-1");
        msg.TextContent.Should().Be("Original text");
        msg.ReasoningText.Should().BeEmpty();
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

    [Fact]
    public async Task HandleMessageUpdated_WhenPartsIsNull_PreservesExistingTextContent()
    {
        // Arrange — message already has text accumulated via deltas
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: "Hello World", completed: false),
        };

        var timeJson = JsonSerializer.SerializeToElement(new { created = 1710576000000L });
        var info = new MessageInfoDto(Id: "msg-1", SessionId: "sess-1", Role: "assistant", Time: timeJson);
        var emptyPartsDto = new MessageWithPartsDto(Info: info, Parts: null);
        var messageUpdatedEvent = new MessageUpdatedEvent { Message = emptyPartsDto };

        // Act
        await TriggerSseEvents(new ChatEvent[] { messageUpdatedEvent }, existingMessages);

        // Assert — null Parts must not overwrite the accumulated text with ""
        var msg = _sut.Messages.First(m => m.Id == "msg-1");
        msg.TextContent.Should().Be("Hello World");
    }

    [Fact]
    public async Task HandleMessageUpdated_WhenPartsIsEmpty_PreservesExistingTextContent()
    {
        // Arrange — message already has text accumulated via deltas
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: "Hello World", completed: false),
        };

        var timeJson = JsonSerializer.SerializeToElement(new { created = 1710576000000L });
        var info = new MessageInfoDto(Id: "msg-1", SessionId: "sess-1", Role: "assistant", Time: timeJson);
        var emptyPartsDto = new MessageWithPartsDto(Info: info, Parts: Array.Empty<PartDto>());
        var messageUpdatedEvent = new MessageUpdatedEvent { Message = emptyPartsDto };

        // Act
        await TriggerSseEvents(new ChatEvent[] { messageUpdatedEvent }, existingMessages);

        // Assert — empty Parts must not overwrite the accumulated text with ""
        var msg = _sut.Messages.First(m => m.Id == "msg-1");
        msg.TextContent.Should().Be("Hello World");
    }

    // ─── Optimistic message reconciliation ───────────────────────────────────────

    [Fact]
    public async Task HandleMessageUpdated_WhenUserMessageArrivesWithServerIdAndOptimisticExists_ReplacesOptimisticMessage()
    {
        // Arrange
        _chatService
            .GetMessagesAsync("sess-1", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Ok(new List<MessageWithPartsDto>()));

        var timeJson = JsonSerializer.SerializeToElement(new { created = 1710576000000L });
        var info = new MessageInfoDto(Id: "server-msg-1", SessionId: "sess-1", Role: "user", Time: timeJson);
        var part = new PartDto(Id: "part-1", SessionId: "sess-1", MessageId: "server-msg-1", Type: "text", Text: "hello");
        var dto = new MessageWithPartsDto(Info: info, Parts: new[] { part });
        var messageUpdatedEvent = new MessageUpdatedEvent { Message = dto };

        _chatService
            .SubscribeToEventsAsync(Arg.Any<CancellationToken>())
            .Returns(YieldEventsWithDelay(100, messageUpdatedEvent));

        // Act — start session, then add optimistic message before SSE fires
        _sut.SetSession("sess-1");

        var optimistic = ChatMessage.CreateOptimistic("sess-1", "hello");
        _sut.Messages.Add(optimistic);

        await Task.Delay(300); // wait for SSE to fire and be processed

        // Assert — optimistic placeholder replaced in-place; no duplicate
        _sut.Messages.Should().HaveCount(1);
        _sut.Messages.First().Id.Should().Be("server-msg-1");
    }

    [Fact]
    public async Task HandleMessageUpdated_WhenUserMessageArrivesWithNullPartsAndOptimisticExists_ReplacesOptimisticMessage()
    {
        // Arrange — simulate the real production scenario:
        // server sends message.updated for user message with Parts = null
        _chatService
            .GetMessagesAsync("sess-1", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Ok(new List<MessageWithPartsDto>()));

        var timeJson = JsonSerializer.SerializeToElement(new { created = 1710576000000L });
        var info = new MessageInfoDto(Id: "server-msg-1", SessionId: "sess-1", Role: "user", Time: timeJson);
        // Parts = null — this is what the server actually sends
        var emptyPartsDto = new MessageWithPartsDto(Info: info, Parts: null);
        var messageUpdatedEvent = new MessageUpdatedEvent { Message = emptyPartsDto };

        _chatService
            .SubscribeToEventsAsync(Arg.Any<CancellationToken>())
            .Returns(YieldEventsWithDelay(100, messageUpdatedEvent));

        // Act
        _sut.SetSession("sess-1");

        // Add optimistic message after session is set (simulating SendMessageAsync)
        var optimistic = ChatMessage.CreateOptimistic("sess-1", "salutami con ciao bello");
        _sut.Messages.Add(optimistic);

        await Task.Delay(300);

        // Assert — optimistic placeholder replaced, not duplicated
        _sut.Messages.Should().HaveCount(1);
        _sut.Messages.First().Id.Should().Be("server-msg-1");
        _sut.Messages.First().IsOptimistic.Should().BeFalse();
    }

    [Fact]
    public async Task HandleMessageUpdated_WhenUserMessageArrivesWithServerIdAndNoOptimisticExists_AddsMessage()
    {
        // Arrange — no existing messages, no optimistic placeholder
        _chatService
            .GetMessagesAsync("sess-1", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Ok(new List<MessageWithPartsDto>()));

        var timeJson = JsonSerializer.SerializeToElement(new { created = 1710576000000L });
        var info = new MessageInfoDto(Id: "server-msg-1", SessionId: "sess-1", Role: "user", Time: timeJson);
        var part = new PartDto(Id: "part-1", SessionId: "sess-1", MessageId: "server-msg-1", Type: "text", Text: "hello");
        var dto = new MessageWithPartsDto(Info: info, Parts: new[] { part });
        var messageUpdatedEvent = new MessageUpdatedEvent { Message = dto };

        _chatService
            .SubscribeToEventsAsync(Arg.Any<CancellationToken>())
            .Returns(YieldEventsWithDelay(100, messageUpdatedEvent));

        // Act
        _sut.SetSession("sess-1");

        await Task.Delay(300); // wait for SSE to fire and be processed

        // Assert — message added normally; exactly one entry
        _sut.Messages.Should().HaveCount(1);
        _sut.Messages.First().Id.Should().Be("server-msg-1");
    }

    // ─── Permission request handling ─────────────────────────────────────────

    [Fact]
    public async Task HandlePermissionRequested_WhenEventArrives_AddsPermissionCardAndSetsPendingFlag()
    {
        // Arrange
        var permissionEvent = BuildPermissionRequestedEvent();

        // Act
        await TriggerSseEvents(new ChatEvent[] { permissionEvent });

        // Assert
        _sut.Messages.Should().ContainSingle(m => m.MessageKind == MessageKind.PermissionRequest);
        _sut.HasPendingPermissions.Should().BeTrue();
    }

    [Fact]
    public async Task HandlePermissionRequested_WhenMultipleEventsArrive_AddsIndependentPermissionCards()
    {
        // Arrange
        var first = BuildPermissionRequestedEvent(id: "per-1");
        var second = BuildPermissionRequestedEvent(id: "per-2", permission: "filesystem");

        // Act
        await TriggerSseEvents(new ChatEvent[] { first, second });

        // Assert
        _sut.Messages.Should().HaveCount(2);
        _sut.Messages.Count(m => m.MessageKind == MessageKind.PermissionRequest).Should().Be(2);
        _sut.HasPendingPermissions.Should().BeTrue();
    }

    [Fact]
    public async Task ReplyToPermissionAsync_WhenApiCallSucceeds_ResolvesMatchingCard()
    {
        // Arrange
        var permissionEvent = BuildPermissionRequestedEvent();
        await TriggerSseEvents(new ChatEvent[] { permissionEvent });

        _apiClient.ReplyToPermissionAsync("per-1", "always", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<bool>.Success(true)));

        // Act
        await _sut.ReplyToPermissionAsync("per-1", "always");

        // Assert
        var card = _sut.Messages.Single(m => m.RequestId == "per-1");
        card.PermissionStatus.Should().Be(PermissionStatus.Resolved);
        card.ResolvedReply.Should().Be("always");
        card.ResolvedReplyLabel.Should().Be("Always");
        _sut.HasPendingPermissions.Should().BeFalse();
        _ = _apiClient.Received(1).ReplyToPermissionAsync("per-1", "always", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplyToPermissionAsync_WhenApiCallFails_KeepsCardPending()
    {
        // Arrange
        var permissionEvent = BuildPermissionRequestedEvent();
        await TriggerSseEvents(new ChatEvent[] { permissionEvent });

        _apiClient.ReplyToPermissionAsync("per-1", "reject", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<bool>.Failure(new OpencodeApiError(ErrorKind.NetworkUnreachable, "offline", null, new HttpRequestException("offline")))));

        // Act
        await _sut.ReplyToPermissionAsync("per-1", "reject");

        // Assert
        var card = _sut.Messages.Single(m => m.RequestId == "per-1");
        card.PermissionStatus.Should().Be(PermissionStatus.Pending);
        card.ResolvedReply.Should().BeNullOrEmpty();
        _sut.HasPendingPermissions.Should().BeTrue();
    }

    [Fact]
    public async Task ReplyToPermissionAsync_WhenApiThrowsException_KeepsCardPendingAndCapturesException()
    {
        // Arrange
        var permissionEvent = BuildPermissionRequestedEvent();
        await TriggerSseEvents(new ChatEvent[] { permissionEvent });

        _apiClient.ReplyToPermissionAsync("per-1", "always", Arg.Any<CancellationToken>())
            .Returns<Task<OpencodeResult<bool>>>(_ => throw new HttpRequestException("network error"));

        // Act — must not throw
        await _sut.ReplyToPermissionAsync("per-1", "always");

        // Assert — card stays pending; no crash
        var card = _sut.Messages.Single(m => m.RequestId == "per-1");
        card.PermissionStatus.Should().Be(PermissionStatus.Pending);
        card.ResolvedReply.Should().BeNullOrEmpty();
        _sut.HasPendingPermissions.Should().BeTrue();
    }

    [Fact]
    public async Task HandlePermissionRequested_WhenSessionIdDiffers_StillAddsPermissionCard()
    {
        // Arrange — permission event carries a different sessionID than the active session
        var permissionEvent = BuildPermissionRequestedEvent(id: "per-x", sessionId: "sess-OTHER");

        // Act
        await TriggerSseEvents(new ChatEvent[] { permissionEvent });

        // Assert — no session filtering on permission events (AC-010)
        _sut.Messages.Should().ContainSingle(m => m.MessageKind == MessageKind.PermissionRequest);
        _sut.HasPendingPermissions.Should().BeTrue();
    }

    /// <summary>
    /// Produces an <see cref="IAsyncEnumerable{ChatEvent}"/> that waits
    /// <paramref name="delayMs"/> milliseconds before yielding each event.
    /// Used to simulate SSE events arriving after an optimistic message is added.
    /// </summary>
    private static async IAsyncEnumerable<ChatEvent> YieldEventsWithDelay(int delayMs, params ChatEvent[] events)
    {
        await Task.Delay(delayMs);
        foreach (var e in events)
        {
            yield return e;
            await Task.Yield();
        }
    }

    // ─── Project directory filtering helpers ──────────────────────────────────

    /// <summary>
    /// Configures <see cref="IActiveProjectService"/> to return a project with the given
    /// worktree, then triggers <see cref="ChatViewModel.LoadContextCommand"/> to populate
    /// the private <c>_currentProjectDirectory</c> field.
    /// </summary>
    private async Task SetCurrentProjectDirectory(string? worktree)
    {
        if (worktree is not null)
        {
            var project = new ProjectDto(
                Id: "proj-1",
                Worktree: worktree,
                VcsDir: null,
                Vcs: null,
                Time: new ProjectTimeDto(Created: 0L, Initialized: null));

            _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>()).Returns(project);
            _activeProjectService.GetCachedWorktree().Returns(worktree);
        }
        else
        {
            _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
                .Returns((ProjectDto?)null);
            _activeProjectService.GetCachedWorktree().Returns((string?)null);
        }

        await _sut.LoadContextCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Sets up the ViewModel with a project directory, session "sess-1", mocks
    /// GetMessagesAsync to return <paramref name="existingMessages"/> (or empty),
    /// mocks SubscribeToEventsAsync to yield <paramref name="events"/>, calls SetSession,
    /// and waits for SSE processing.
    /// </summary>
    private async Task TriggerSseEventsWithProjectDirectory(
        string? projectDirectory,
        ChatEvent[] events,
        List<MessageWithPartsDto>? existingMessages = null)
    {
        await SetCurrentProjectDirectory(projectDirectory);

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

    // ─── HandleMessageUpdated — ProjectDirectory filtering ────────────────────

    [Fact]
    public async Task HandleMessageUpdated_WhenProjectDirectoryMatches_ProcessesEvent()
    {
        // Arrange
        var newDto = BuildMessageDto(
            id: "msg-new",
            sessionId: "sess-1",
            role: "assistant",
            text: "Hello from matching project");
        var messageUpdatedEvent = new MessageUpdatedEvent
        {
            Message = newDto,
            ProjectDirectory = "/home/user/my-project",
        };

        // Act
        await TriggerSseEventsWithProjectDirectory(
            "/home/user/my-project",
            new ChatEvent[] { messageUpdatedEvent });

        // Assert — event processed because ProjectDirectory matches _currentProjectDirectory
        _sut.Messages.Should().ContainSingle(m => m.Id == "msg-new");
        _sut.Messages.First().TextContent.Should().Be("Hello from matching project");
    }

    [Fact]
    public async Task HandleMessageUpdated_WhenProjectDirectoryMismatches_DiscardsEvent()
    {
        // Arrange
        var newDto = BuildMessageDto(
            id: "msg-new",
            sessionId: "sess-1",
            role: "assistant",
            text: "Should not appear");
        var messageUpdatedEvent = new MessageUpdatedEvent
        {
            Message = newDto,
            ProjectDirectory = "/home/user/other-project",
        };

        // Act
        await TriggerSseEventsWithProjectDirectory(
            "/home/user/my-project",
            new ChatEvent[] { messageUpdatedEvent });

        // Assert — event discarded because ProjectDirectory does not match _currentProjectDirectory
        _sut.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleMessageUpdated_WhenProjectDirectoryIsNull_ProcessesEvent()
    {
        // Arrange — event has no ProjectDirectory (null), so the filter is skipped
        var newDto = BuildMessageDto(
            id: "msg-new",
            sessionId: "sess-1",
            role: "assistant",
            text: "Hello from null directory");
        var messageUpdatedEvent = new MessageUpdatedEvent
        {
            Message = newDto,
            ProjectDirectory = null,
        };

        // Act
        await TriggerSseEventsWithProjectDirectory(
            "/home/user/my-project",
            new ChatEvent[] { messageUpdatedEvent });

        // Assert — event processed because null ProjectDirectory skips the filter
        _sut.Messages.Should().ContainSingle(m => m.Id == "msg-new");
        _sut.Messages.First().TextContent.Should().Be("Hello from null directory");
    }

    // ─── HandlePermissionReplied ──────────────────────────────────────────────

    private static PermissionRepliedEvent MakePermissionRepliedEvent(
        string sessionId, string requestId, string reply, string? projectDirectory = null)
        => new()
        {
            SessionId = sessionId,
            RequestId = requestId,
            Reply = reply,
            ProjectDirectory = projectDirectory,
        };

    private static MessageRemovedEvent MakeMessageRemovedEvent(
        string sessionId, string messageId, string? projectDirectory = null)
        => new()
        {
            SessionId = sessionId,
            MessageId = messageId,
            ProjectDirectory = projectDirectory,
        };

    private static MessagePartRemovedEvent MakeMessagePartRemovedEvent(
        string sessionId, string messageId, string partId, string? projectDirectory = null)
        => new()
        {
            SessionId = sessionId,
            MessageId = messageId,
            PartId = partId,
            ProjectDirectory = projectDirectory,
        };

    private static SessionCreatedEvent MakeSessionCreatedEvent(string sessionId, string projectId, string title = "Test")
    {
        var session = new openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto(
            Id: sessionId,
            ProjectId: projectId,
            Directory: "/dir",
            ParentId: null,
            Summary: null,
            Share: null,
            Title: title,
            Version: "1.0",
            Time: new SessionTimeDto(Created: 0L, Updated: 1000L, Compacting: null),
            Revert: null);
        return new SessionCreatedEvent
        {
            SessionId = sessionId,
            Session = session,
        };
    }

    private static SessionDeletedEvent MakeSessionDeletedEvent(string sessionId, string projectId)
        => new()
        {
            SessionId = sessionId,
            ProjectId = projectId,
        };

    [Fact]
    public async Task HandlePermissionReplied_WhenReplyIsOnce_ResolvesMatchingCard()
    {
        // Arrange — inject both the permission request and the replied event in a single SSE stream.
        // SetSession is a no-op if the session ID is already set, so both events must arrive
        // in the same subscription to be processed together.
        var permissionEvent = BuildPermissionRequestedEvent(id: "req-1");
        var repliedEvent = MakePermissionRepliedEvent("sess-1", "req-1", "once");

        // Act
        await TriggerSseEvents(new ChatEvent[] { permissionEvent, repliedEvent });

        // Assert
        var card = _sut.Messages.Single(m => m.MessageKind == MessageKind.PermissionRequest);
        card.PermissionStatus.Should().Be(PermissionStatus.Resolved);
        card.ResolvedReplyLabel.Should().Be("Once");
    }

    [Fact]
    public async Task HandlePermissionReplied_WhenReplyIsReject_ResolvesAllPendingCards()
    {
        // Arrange — inject two permission requests and a reject reply in a single SSE stream.
        // A reject reply cascades to ALL pending permission cards in the session.
        var first = BuildPermissionRequestedEvent(id: "req-1");
        var second = BuildPermissionRequestedEvent(id: "req-2", permission: "filesystem");
        var repliedEvent = MakePermissionRepliedEvent("sess-1", "req-1", "reject");

        // Act
        await TriggerSseEvents(new ChatEvent[] { first, second, repliedEvent });

        // Assert — both cards resolved with "Deny"
        var cards = _sut.Messages.Where(m => m.MessageKind == MessageKind.PermissionRequest).ToList();
        cards.Should().HaveCount(2);
        cards.Should().AllSatisfy(c =>
        {
            c.PermissionStatus.Should().Be(PermissionStatus.Resolved);
            c.ResolvedReplyLabel.Should().Be("Deny");
        });
    }

    [Fact]
    public async Task HandlePermissionReplied_WhenProjectDirectoryMismatches_DoesNotResolveCard()
    {
        // Arrange — inject a permission request and a replied event with a mismatching project directory
        // in a single SSE stream. The project directory filter must block the replied event.
        var permissionEvent = BuildPermissionRequestedEvent(id: "req-1");
        var repliedEvent = MakePermissionRepliedEvent("sess-1", "req-1", "once", projectDirectory: "/project/b");

        // Act — set project directory to "/project/a" so the replied event is filtered out
        await TriggerSseEventsWithProjectDirectory(
            "/project/a",
            new ChatEvent[] { permissionEvent, repliedEvent });

        // Assert — card is still pending because project directory did not match
        var card = _sut.Messages.Single(m => m.MessageKind == MessageKind.PermissionRequest);
        card.PermissionStatus.Should().Be(PermissionStatus.Pending);
    }

    // ─── HandleMessageRemoved ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleMessageRemoved_WhenMessageExists_RemovesMessageFromCollection()
    {
        // Arrange — load two messages
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: "First"),
            BuildMessageDto(id: "msg-2", sessionId: "sess-1", role: "assistant", text: "Second"),
        };
        var removedEvent = MakeMessageRemovedEvent("sess-1", "msg-1");

        // Act
        await TriggerSseEvents(new ChatEvent[] { removedEvent }, existingMessages);

        // Assert
        _sut.Messages.Should().HaveCount(1);
        _sut.Messages.Single().Id.Should().Be("msg-2");
    }

    [Fact]
    public async Task HandleMessageRemoved_WhenProjectDirectoryMismatches_DoesNotRemoveMessage()
    {
        // Arrange — load one message, then inject a remove event from a different project
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: "Hello"),
        };
        var removedEvent = MakeMessageRemovedEvent("sess-1", "msg-1", projectDirectory: "/other/project");

        // Act
        await TriggerSseEventsWithProjectDirectory(
            "/my/project",
            new ChatEvent[] { removedEvent },
            existingMessages);

        // Assert — message not removed because project directory did not match
        _sut.Messages.Should().HaveCount(1);
    }

    // ─── HandleMessagePartRemoved ─────────────────────────────────────────────

    [Fact]
    public async Task HandleMessagePartRemoved_WhenToolCallExists_RemovesToolCallFromMessage()
    {
        // Arrange — load an assistant message and inject a tool part to create a ToolCallInfo
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: ""),
        };

        var stateJson = JsonSerializer.SerializeToElement(new { status = "pending" });
        var toolPart = new PartDto(
            Id: "part-tool-1",
            SessionId: "sess-1",
            MessageId: "msg-1",
            Type: "tool",
            Text: null)
        {
            ToolName = "bash",
            State = stateJson,
        };
        var toolPartEvent = new MessagePartUpdatedEvent { Part = toolPart };
        var removedEvent = MakeMessagePartRemovedEvent("sess-1", "msg-1", "part-tool-1");

        // Act — first inject the tool part to create the ToolCallInfo, then remove it
        await TriggerSseEvents(new ChatEvent[] { toolPartEvent, removedEvent }, existingMessages);

        // Assert
        _sut.Messages.Should().HaveCount(1);
        _sut.Messages[0].ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleMessagePartRemoved_WhenPartIdDoesNotExist_DoesNotThrowAndPreservesMessage()
    {
        // Arrange — load an assistant message with no tool calls
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: "Hello"),
        };
        var removedEvent = MakeMessagePartRemovedEvent("sess-1", "msg-1", "non-existent-part");

        // Act — must not throw
        await TriggerSseEvents(new ChatEvent[] { removedEvent }, existingMessages);

        // Assert — message still present, no crash
        _sut.Messages.Should().HaveCount(1);
    }

    // ─── HandleSessionCreated ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleSessionCreated_WhenEventReceived_PublishesSessionCreatedMessage()
    {
        // Arrange
        SessionCreatedMessage? received = null;
        WeakReferenceMessenger.Default.Register<SessionCreatedMessage>(
            this, (_, msg) => received = msg);

        var sessionEvent = MakeSessionCreatedEvent("sess-new", "proj-1", "New Session");

        // Act
        await TriggerSseEvents(new ChatEvent[] { sessionEvent });

        // Assert
        received.Should().NotBeNull();
        received!.SessionId.Should().Be("sess-new");
        received.ProjectId.Should().Be("proj-1");
        received.Title.Should().Be("New Session");

        // Cleanup
        WeakReferenceMessenger.Default.Unregister<SessionCreatedMessage>(this);
    }

    // ─── HandleSessionDeleted ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleSessionDeleted_WhenEventReceived_PublishesSessionDeletedMessage()
    {
        // Arrange
        SessionDeletedMessage? received = null;
        WeakReferenceMessenger.Default.Register<SessionDeletedMessage>(
            this, (_, msg) => received = msg);

        var sessionEvent = MakeSessionDeletedEvent("sess-old", "proj-1");

        // Act
        await TriggerSseEvents(new ChatEvent[] { sessionEvent });

        // Assert
        received.Should().NotBeNull();
        received!.SessionId.Should().Be("sess-old");
        received.ProjectId.Should().Be("proj-1");

        // Cleanup
        WeakReferenceMessenger.Default.Unregister<SessionDeletedMessage>(this);
    }

    // ─── HandleMessagePartUpdated — tool part upsert ──────────────────────────

    [Fact]
    public async Task HandleMessagePartUpdated_WhenToolPartArrives_CreatesNewToolCallInfo()
    {
        // Arrange
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: ""),
        };

        var stateJson = JsonSerializer.SerializeToElement(new { status = "pending" });
        var part = new PartDto(
            Id: "part-tool-1",
            SessionId: "sess-1",
            MessageId: "msg-1",
            Type: "tool",
            Text: null)
        {
            ToolName = "bash",
            State = stateJson,
        };
        var toolEvent = new MessagePartUpdatedEvent { Part = part };

        // Act
        await TriggerSseEvents(new ChatEvent[] { toolEvent }, existingMessages);

        // Assert
        _sut.Messages.Should().HaveCount(1);
        _sut.Messages[0].ToolCalls.Should().HaveCount(1);
        _sut.Messages[0].ToolCalls[0].ToolName.Should().Be("bash");
        _sut.Messages[0].ToolCalls[0].Status.Should().Be(ToolCallStatus.Pending);
    }

    [Fact]
    public async Task HandleMessagePartUpdated_WhenToolPartArrivesAgain_UpsertExistingToolCallInfo()
    {
        // Arrange — first inject a pending tool part, then update it to completed
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: ""),
        };

        var pendingStateJson = JsonSerializer.SerializeToElement(new { status = "pending" });
        var pendingPart = new PartDto(
            Id: "part-tool-1",
            SessionId: "sess-1",
            MessageId: "msg-1",
            Type: "tool",
            Text: null)
        {
            ToolName = "bash",
            State = pendingStateJson,
        };

        var completedStateJson = JsonSerializer.SerializeToElement(new
        {
            status = "completed",
            output = "result text",
            title = "Ran bash",
            time = new { start = 1000L, end = 1500L }
        });
        var completedPart = new PartDto(
            Id: "part-tool-1",
            SessionId: "sess-1",
            MessageId: "msg-1",
            Type: "tool",
            Text: null)
        {
            ToolName = "bash",
            State = completedStateJson,
        };

        var pendingEvent = new MessagePartUpdatedEvent { Part = pendingPart };
        var completedEvent = new MessagePartUpdatedEvent { Part = completedPart };

        // Act
        await TriggerSseEvents(new ChatEvent[] { pendingEvent, completedEvent }, existingMessages);

        // Assert — upsert, not duplicate
        _sut.Messages[0].ToolCalls.Should().HaveCount(1);
        _sut.Messages[0].ToolCalls[0].Status.Should().Be(ToolCallStatus.Completed);
        _sut.Messages[0].ToolCalls[0].Output.Should().Be("result text");
        _sut.Messages[0].ToolCalls[0].Title.Should().Be("Ran bash");
        _sut.Messages[0].ToolCalls[0].DurationMs.Should().Be(500L);
    }

    // ─── HandleMessagePartUpdated — reasoning part ────────────────────────────

    [Fact]
    public async Task HandleMessagePartUpdated_WhenReasoningPartArrives_SetsReasoningText()
    {
        // Arrange
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: ""),
        };

        var part = new PartDto(
            Id: "part-reasoning-1",
            SessionId: "sess-1",
            MessageId: "msg-1",
            Type: "reasoning",
            Text: "I am thinking about this...");
        var reasoningEvent = new MessagePartUpdatedEvent { Part = part };

        // Act
        await TriggerSseEvents(new ChatEvent[] { reasoningEvent }, existingMessages);

        // Assert
        _sut.Messages[0].ReasoningText.Should().Be("I am thinking about this...");
        _sut.Messages[0].HasReasoning.Should().BeTrue();
    }

    // ─── HandleMessagePartDelta — reasoning field ─────────────────────────────

    [Fact]
    public async Task HandleMessagePartDelta_WhenFieldIsReasoning_AppendsToReasoningText()
    {
        // Arrange — message already has some reasoning text
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: ""),
        };

        // First set initial reasoning text via a part update
        var initialPart = new PartDto(
            Id: "part-1",
            SessionId: "sess-1",
            MessageId: "msg-1",
            Type: "reasoning",
            Text: "think");
        var initialEvent = new MessagePartUpdatedEvent { Part = initialPart };

        var deltaEvent = new MessagePartDeltaEvent
        {
            SessionId = "sess-1",
            MessageId = "msg-1",
            PartId = "part-1",
            Field = "reasoning",
            Delta = "ing more",
        };

        // Act
        await TriggerSseEvents(new ChatEvent[] { initialEvent, deltaEvent }, existingMessages);

        // Assert
        _sut.Messages[0].ReasoningText.Should().Be("thinking more");
    }

    // ─── HandleMessagePartUpdated — step-start ────────────────────────────────

    [Fact]
    public async Task HandleMessagePartUpdated_WhenStepStartArrives_IncrementsStepCount()
    {
        // Arrange
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: ""),
        };

        var stepStartPart1 = new PartDto(
            Id: "part-step-1",
            SessionId: "sess-1",
            MessageId: "msg-1",
            Type: "step-start",
            Text: null);
        var stepStartPart2 = new PartDto(
            Id: "part-step-2",
            SessionId: "sess-1",
            MessageId: "msg-1",
            Type: "step-start",
            Text: null);

        var stepEvent1 = new MessagePartUpdatedEvent { Part = stepStartPart1 };
        var stepEvent2 = new MessagePartUpdatedEvent { Part = stepStartPart2 };

        // Act
        await TriggerSseEvents(new ChatEvent[] { stepEvent1, stepEvent2 }, existingMessages);

        // Assert
        _sut.Messages[0].StepCount.Should().Be(2);
    }

    // ─── HandleMessagePartUpdated — step-finish ───────────────────────────────

    [Fact]
    public async Task HandleMessagePartUpdated_WhenStepFinishArrives_SetsLastStepCost()
    {
        // Arrange
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: ""),
        };

        var extras = new Dictionary<string, JsonElement>
        {
            ["cost"] = JsonSerializer.SerializeToElement(0.0042m)
        };
        var stepFinishPart = new PartDto(
            Id: "part-step-finish-1",
            SessionId: "sess-1",
            MessageId: "msg-1",
            Type: "step-finish",
            Text: null)
        {
            Extras = extras
        };
        var stepFinishEvent = new MessagePartUpdatedEvent { Part = stepFinishPart };

        // Act
        await TriggerSseEvents(new ChatEvent[] { stepFinishEvent }, existingMessages);

        // Assert
        _sut.Messages[0].LastStepCost.Should().Be(0.0042m);
    }

    // ─── HandleMessagePartUpdated — subtask part ──────────────────────────────

    [Fact]
    public async Task HandleMessagePartUpdated_WhenSubtaskPartArrives_AppendsToSubtaskLabels()
    {
        // Arrange
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: ""),
        };

        var extras = new Dictionary<string, JsonElement>
        {
            ["agent"] = JsonSerializer.SerializeToElement("agent-name"),
            ["description"] = JsonSerializer.SerializeToElement("task description"),
        };
        var part = new PartDto("part-sub-1", "sess-1", "msg-1", "subtask", null) { Extras = extras };
        var subtaskEvent = new MessagePartUpdatedEvent { Part = part };

        // Act
        await TriggerSseEvents(new ChatEvent[] { subtaskEvent }, existingMessages);

        // Assert
        _sut.Messages[0].SubtaskLabels.Should().HaveCount(1);
        _sut.Messages[0].SubtaskLabels[0].Should().Be("agent-name: task description");
    }

    // ─── HandleMessagePartUpdated — agent part ────────────────────────────────

    [Fact]
    public async Task HandleMessagePartUpdated_WhenAgentPartArrives_AppendsAgentNameToSubtaskLabels()
    {
        // Arrange
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: ""),
        };

        var extras = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("my-agent"),
        };
        var part = new PartDto("part-agent-1", "sess-1", "msg-1", "agent", null) { Extras = extras };
        var agentEvent = new MessagePartUpdatedEvent { Part = part };

        // Act
        await TriggerSseEvents(new ChatEvent[] { agentEvent }, existingMessages);

        // Assert
        _sut.Messages[0].SubtaskLabels.Should().HaveCount(1);
        _sut.Messages[0].SubtaskLabels[0].Should().Be("my-agent");
    }

    // ─── HandleMessagePartUpdated — compaction part ───────────────────────────

    [Fact]
    public async Task HandleMessagePartUpdated_WhenCompactionPartArrives_SetsCompactionNotice()
    {
        // Arrange
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: ""),
        };

        var part = new PartDto("part-compact-1", "sess-1", "msg-1", "compaction", null);
        var compactionEvent = new MessagePartUpdatedEvent { Part = part };

        // Act
        await TriggerSseEvents(new ChatEvent[] { compactionEvent }, existingMessages);

        // Assert
        _sut.Messages[0].CompactionNotice.Should().Be("Context compacted");
    }

    [Fact]
    public async Task HandleMessagePartUpdated_WhenAutoCompactionPartArrives_SetsAutoCompactionNotice()
    {
        // Arrange
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: ""),
        };

        var extras = new Dictionary<string, JsonElement>
        {
            ["auto"] = JsonSerializer.SerializeToElement(true),
        };
        var part = new PartDto("part-compact-auto-1", "sess-1", "msg-1", "compaction", null) { Extras = extras };
        var compactionEvent = new MessagePartUpdatedEvent { Part = part };

        // Act
        await TriggerSseEvents(new ChatEvent[] { compactionEvent }, existingMessages);

        // Assert
        _sut.Messages[0].CompactionNotice.Should().Be("Context auto-compacted");
    }

    // ─── HandleMessagePartUpdated — ignored part types ────────────────────────

    [Theory]
    [InlineData("snapshot")]
    [InlineData("patch")]
    [InlineData("retry")]
    public async Task HandleMessagePartUpdated_WhenIgnoredPartTypeArrives_DoesNotMutateMessage(string partType)
    {
        // Arrange
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: "Known text"),
        };

        var part = new PartDto($"part-{partType}-1", "sess-1", "msg-1", partType, null);
        var ignoredEvent = new MessagePartUpdatedEvent { Part = part };

        // Act
        await TriggerSseEvents(new ChatEvent[] { ignoredEvent }, existingMessages);

        // Assert
        _sut.Messages[0].TextContent.Should().Be("Known text");
        _sut.Messages[0].ToolCalls.Should().BeEmpty();
        _sut.Messages[0].ReasoningText.Should().BeEmpty();
        _sut.Messages[0].StepCount.Should().Be(0);
    }

    // ─── HandleMessageUpdated — tool parts upsert (REQ-018) ──────────────────

    [Fact]
    public async Task HandleMessageUpdated_WhenMessageHasToolParts_UpsertsToolCallsIntoMessage()
    {
        // Arrange
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: ""),
        };

        var stateJson = JsonSerializer.SerializeToElement(new { status = "completed", output = "result", title = "Done" });
        var toolPart = new PartDto("part-tool-1", "sess-1", "msg-1", "tool", null)
        {
            ToolName = "bash",
            State = stateJson,
        };
        var messageDto = new MessageWithPartsDto(
            Info: new MessageInfoDto("msg-1", "sess-1", "assistant", JsonSerializer.SerializeToElement(new { created = 0L })),
            Parts: new[] { toolPart });
        var msgEvent = new MessageUpdatedEvent { Message = messageDto, ProjectDirectory = null };

        // Act
        await TriggerSseEvents(new ChatEvent[] { msgEvent }, existingMessages);

        // Assert
        _sut.Messages[0].ToolCalls.Should().HaveCount(1);
        _sut.Messages[0].ToolCalls[0].Status.Should().Be(ToolCallStatus.Completed);
        _sut.Messages[0].ToolCalls[0].Output.Should().Be("result");
    }

    // ─── HandleSessionCreated — empty title fallback ──────────────────────────

    [Fact]
    public async Task HandleSessionCreated_WhenSessionTitleIsEmpty_PublishesNewSessionFallbackTitle()
    {
        // Arrange
        SessionCreatedMessage? received = null;
        WeakReferenceMessenger.Default.Register<SessionCreatedMessage>(this, (_, msg) => received = msg);

        var sessionEvent = MakeSessionCreatedEvent("sess-new", "proj-1", title: "");

        // Act
        await TriggerSseEvents(new ChatEvent[] { sessionEvent });

        // Assert
        received.Should().NotBeNull();
        received!.Title.Should().Be("New Session");

        // Cleanup
        WeakReferenceMessenger.Default.Unregister<SessionCreatedMessage>(this);
    }

    // ─── HandleMessagePartRemoved — project directory filter ─────────────────

    [Fact]
    public async Task HandleMessagePartRemoved_WhenProjectDirectoryMismatches_DoesNotRemoveToolCall()
    {
        // Arrange — load an assistant message, then deliver both the tool-part creation event
        // and the mismatching remove event in a SINGLE SSE subscription so that SetSession is
        // only called once and both events are actually processed by the same handler.
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: ""),
        };

        var stateJson = JsonSerializer.SerializeToElement(new { status = "pending" });
        var toolPart = new PartDto(
            Id: "part-tool-1",
            SessionId: "sess-1",
            MessageId: "msg-1",
            Type: "tool",
            Text: null)
        {
            ToolName = "bash",
            State = stateJson,
        };

        // toolPartEvent carries the matching project directory — it will be processed
        var toolPartEvent = new MessagePartUpdatedEvent
        {
            Part = toolPart,
            ProjectDirectory = "/project/a",
        };

        // removedEvent carries a mismatching project directory — it must be filtered out
        var removedEvent = MakeMessagePartRemovedEvent("sess-1", "msg-1", "part-tool-1", projectDirectory: "/project/b");

        // Act — both events delivered in a single subscription with project dir = /project/a
        await TriggerSseEventsWithProjectDirectory(
            "/project/a",
            new ChatEvent[] { toolPartEvent, removedEvent },
            existingMessages);

        // Assert — tool call still present because /project/b ≠ /project/a
        _sut.Messages.Should().HaveCount(1);
        _sut.Messages[0].ToolCalls.Should().HaveCount(1);
    }

    // ─── HandlePermissionReplied — session ID filter ──────────────────────────

    [Fact]
    public async Task HandlePermissionReplied_WhenSessionIdDiffers_DoesNotResolveCard()
    {
        // Arrange — inject a permission request for sess-1, then a replied event for a different session
        var permissionEvent = BuildPermissionRequestedEvent(id: "req-1", sessionId: "sess-1");
        var repliedEvent = MakePermissionRepliedEvent("sess-OTHER", "req-1", "once");

        // Act
        await TriggerSseEvents(new ChatEvent[] { permissionEvent, repliedEvent });

        // Assert — card is still pending because session ID did not match
        var card = _sut.Messages.Single(m => m.MessageKind == MessageKind.PermissionRequest);
        card.PermissionStatus.Should().Be(PermissionStatus.Pending);
    }

    // ─── ShowUnhandledSseEvents — default state ───────────────────────────────

    [Fact]
    public void ShowUnhandledSseEvents_OnFreshInstance_IsFalseByDefault()
    {
        // Arrange — fresh SUT from constructor (already created in test class constructor)

        // Act — no action needed

        // Assert
        _sut.ShowUnhandledSseEvents.Should().BeFalse();
    }

    // ─── HandleUnknownEvent ───────────────────────────────────────────────────────

    [Fact]
    public async Task HandleUnknownEvent_WhenUnknownEventReceived_AddsFallbackMessageToMessages()
    {
        // Arrange
        _sut.ShowUnhandledSseEvents = true;
        var unknownEvent = new UnknownEvent
        {
            RawType = "some.unknown.event",
            RawData = null,
        };

        // Act
        await TriggerSseEvents(new ChatEvent[] { unknownEvent });

        // Assert
        _sut.Messages.Should().ContainSingle(m => m.SenderType == SenderType.Fallback);
        var fallback = _sut.Messages.Single(m => m.SenderType == SenderType.Fallback);
        fallback.MessageKind.Should().Be(MessageKind.Standard);
    }

    [Fact]
    public async Task HandleUnknownEvent_WhenUnknownEventReceived_FallbackMessageHasCorrectRawType()
    {
        // Arrange
        _sut.ShowUnhandledSseEvents = true;
        var unknownEvent = new UnknownEvent
        {
            RawType = "some.unknown.event",
            RawData = null,
        };

        // Act
        await TriggerSseEvents(new ChatEvent[] { unknownEvent });

        // Assert — tests run in DEBUG, so FallbackRawType is populated
        var fallback = _sut.Messages.Single(m => m.SenderType == SenderType.Fallback);
        fallback.FallbackRawType.Should().Be("some.unknown.event");
    }

    [Fact]
    public async Task HandleUnknownEvent_WhenRawDataIsNull_FallbackRawJsonIsNull()
    {
        // Arrange
        _sut.ShowUnhandledSseEvents = true;
        var unknownEvent = new UnknownEvent
        {
            RawType = "some.unknown.event",
            RawData = null,
        };

        // Act
        await TriggerSseEvents(new ChatEvent[] { unknownEvent });

        // Assert
        var fallback = _sut.Messages.Single(m => m.SenderType == SenderType.Fallback);
        fallback.FallbackRawJson.Should().BeNull();
    }

    [Fact]
    public async Task HandleUnknownEvent_WhenRawDataPresent_FallbackRawJsonIsPopulated()
    {
        // Arrange
        _sut.ShowUnhandledSseEvents = true;
        var rawData = System.Text.Json.JsonSerializer.SerializeToElement(new { foo = 1 });
        var unknownEvent = new UnknownEvent
        {
            RawType = "some.unknown.event",
            RawData = rawData,
        };

        // Act
        await TriggerSseEvents(new ChatEvent[] { unknownEvent });

        // Assert — tests run in DEBUG, so FallbackRawJson is populated
        var fallback = _sut.Messages.Single(m => m.SenderType == SenderType.Fallback);
        fallback.FallbackRawJson.Should().NotBeNullOrEmpty();
        fallback.FallbackRawJson.Should().Contain("foo");
    }

    [Fact]
    public async Task HandleUnknownEvent_WhenMultipleUnknownEvents_AddsMultipleFallbackMessages()
    {
        // Arrange
        _sut.ShowUnhandledSseEvents = true;
        var event1 = new UnknownEvent { RawType = "event.one", RawData = null };
        var event2 = new UnknownEvent { RawType = "event.two", RawData = null };

        // Act
        await TriggerSseEvents(new ChatEvent[] { event1, event2 });

        // Assert
        _sut.Messages.Count(m => m.SenderType == SenderType.Fallback).Should().Be(2);
    }

    [Fact]
    public async Task HandleUnknownEvent_FallbackMessage_IsNotFromUser()
    {
        // Arrange
        _sut.ShowUnhandledSseEvents = true;
        var unknownEvent = new UnknownEvent { RawType = "some.event", RawData = null };

        // Act
        await TriggerSseEvents(new ChatEvent[] { unknownEvent });

        // Assert
        var fallback = _sut.Messages.Single(m => m.SenderType == SenderType.Fallback);
        fallback.IsFromUser.Should().BeFalse();
    }

    [Fact]
    public async Task HandleUnknownEvent_WhenShowUnhandledSseEventsFalse_DoesNotAddCardToMessages()
    {
        // Arrange
        _sut.ShowUnhandledSseEvents = false;
        var unknownEvent = new UnknownEvent
        {
            RawType = "unknown.test",
            RawData = null,
        };

        // Act
        await TriggerSseEvents(new ChatEvent[] { unknownEvent });

        // Assert
        _sut.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleUnknownEvent_WhenShowUnhandledSseEventsTrue_AddsCardToMessages()
    {
        // Arrange
        _sut.ShowUnhandledSseEvents = true;
        var unknownEvent = new UnknownEvent
        {
            RawType = "unknown.test",
            RawData = null,
        };

        // Act
        await TriggerSseEvents(new ChatEvent[] { unknownEvent });

        // Assert
        _sut.Messages.Should().HaveCount(1);
        _sut.Messages.Single().SenderType.Should().Be(SenderType.Fallback);
    }

    [Fact]
    public void ProjectPreferenceChangedMessage_WhenReceived_UpdatesShowUnhandledSseEvents()
    {
        // Arrange
        _sut.ShowUnhandledSseEvents = false;
        _sut.CurrentProjectId = "proj-1";

        var updatedPref = new ProjectPreference
        {
            ProjectId = "proj-1",
            ShowUnhandledSseEvents = true,
        };

        // Act
        WeakReferenceMessenger.Default.Send(
            new ProjectPreferenceChangedMessage("proj-1", updatedPref));

        // Assert
        _sut.ShowUnhandledSseEvents.Should().BeTrue();
    }

    // ─── HandlePermissionRequested — auto-accept ──────────────────────────────

    [Fact]
    public async Task HandlePermissionRequested_WhenAutoAcceptIsTrue_CallsReplyToPermissionWithAlwaysAndDoesNotAddCard()
    {
        // Arrange
        _sut.AutoAccept = true;
        _apiClient
            .ReplyToPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<bool>.Success(true)));
        var permissionEvent = BuildPermissionRequestedEvent(id: "per-1");

        // Act
        await TriggerSseEvents(new ChatEvent[] { permissionEvent });

        // Assert
        _sut.Messages.Should().NotContain(m => m.MessageKind == MessageKind.PermissionRequest);
        await _apiClient.Received(1).ReplyToPermissionAsync("per-1", "always", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandlePermissionRequested_WhenAutoAcceptIsTrue_DoesNotSetHasPendingPermissions()
    {
        // Arrange
        _sut.AutoAccept = true;
        _apiClient
            .ReplyToPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<bool>.Success(true)));
        var permissionEvent = BuildPermissionRequestedEvent(id: "per-1");

        // Act
        await TriggerSseEvents(new ChatEvent[] { permissionEvent });

        // Assert
        _sut.HasPendingPermissions.Should().BeFalse();
        _sut.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task HandlePermissionRequested_WhenAutoAcceptIsFalse_RendersPermissionCardAsNormal()
    {
        // Arrange
        _sut.AutoAccept = false;
        var permissionEvent = BuildPermissionRequestedEvent(id: "per-1");

        // Act
        await TriggerSseEvents(new ChatEvent[] { permissionEvent });

        // Assert
        _sut.Messages.Should().ContainSingle(m => m.MessageKind == MessageKind.PermissionRequest);
        _sut.HasPendingPermissions.Should().BeTrue();
        await _apiClient.DidNotReceive().ReplyToPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandlePermissionRequested_WhenAutoAcceptIsTrueAndProjectDirectoryMismatches_DiscardsEvent()
    {
        // Arrange
        _sut.AutoAccept = true;
        _apiClient
            .ReplyToPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<bool>.Success(true)));
        var permissionEvent = BuildPermissionRequestedEvent(id: "per-1") with
        {
            ProjectDirectory = "/other/project",
        };

        // Act
        await TriggerSseEventsWithProjectDirectory("/my/project", new ChatEvent[] { permissionEvent });

        // Assert
        _sut.Messages.Should().BeEmpty();
        await _apiClient.DidNotReceive().ReplyToPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandlePermissionRequested_WhenAutoAcceptIsTrueAndApiThrows_DoesNotAddCardAndDoesNotCrash()
    {
        // Arrange
        _sut.AutoAccept = true;
        _apiClient
            .ReplyToPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<OpencodeResult<bool>>>(_ => throw new HttpRequestException("network error"));
        var permissionEvent = BuildPermissionRequestedEvent(id: "per-1");

        // Act — must NOT throw
        await TriggerSseEvents(new ChatEvent[] { permissionEvent });

        // Assert
        _sut.Messages.Should().BeEmpty();
        _sut.HasPendingPermissions.Should().BeFalse();
    }

    /// <summary>
    /// AC-006 — Duplicate requestId guard via <c>_inFlightPermissionReplies</c>.
    ///
    /// The guard prevents duplicate calls for the same requestId. When two SSE events with the
    /// same requestId arrive sequentially, the first event fires a <c>Task.Run</c> and adds the
    /// requestId to the in-flight set. The second event arrives while the first <c>Task.Run</c>
    /// is still in-flight (the <c>finally</c> that removes the requestId has not yet executed),
    /// so the guard blocks it. The API is therefore called exactly once, and no permission card
    /// is added in either case.
    ///
    /// If the guard needs to cover sequential re-arrivals after the first reply completes, the
    /// implementation would need a persistent "already replied" set (not removed in finally).
    /// That is a separate design decision outside this spec's scope.
    /// </summary>
    [Fact]
    public async Task HandlePermissionRequested_WhenAutoAcceptIsTrueAndSameRequestIdArrivesSequentially_DoesNotAddCardEitherTime()
    {
        // Arrange
        _sut.AutoAccept = true;
        _apiClient
            .ReplyToPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<bool>.Success(true)));
        var event1 = BuildPermissionRequestedEvent(id: "per-1");
        var event2 = BuildPermissionRequestedEvent(id: "per-1");

        // Act
        await TriggerSseEvents(new ChatEvent[] { event1, event2 });

        // Assert — no permission card is added for either event
        _sut.Messages.Should().BeEmpty();
        _sut.HasPendingPermissions.Should().BeFalse();
        // The second event is blocked by the in-flight guard (the first Task.Run has not yet
        // completed its finally block), so the API is called exactly once.
        await _apiClient.Received(1).ReplyToPermissionAsync("per-1", "always", Arg.Any<CancellationToken>());
    }

    // ─── HandleQuestionRequested — SSE handler ────────────────────────────────

    /// <summary>
    /// Builds a typed question requested event for the SSE stream.
    /// </summary>
    private static QuestionRequestedEvent BuildQuestionRequestedEvent(
        string id = "q-1",
        string sessionId = "sess-1",
        string question = "Which option?",
        string[]? options = null,
        bool allowFreeText = true,
        string? projectDirectory = null,
        string? toolCallId = null)
    {
        return new QuestionRequestedEvent
        {
            Id = id,
            SessionId = sessionId,
            Question = question,
            Options = options ?? ["Option A", "Option B"],
            AllowFreeText = allowFreeText,
            ProjectDirectory = projectDirectory,
            ToolCallId = toolCallId,
        };
    }

    [Fact]
    public async Task QuestionRequestedEvent_WhenEventMatchesCurrentSession_AddsQuestionCardToMessages()
    {
        // Arrange
        var questionEvent = BuildQuestionRequestedEvent(id: "q-1", sessionId: "sess-1");

        // Act
        await TriggerSseEvents(new ChatEvent[] { questionEvent });

        // Assert
        _sut.Messages.Should().ContainSingle(m => m.MessageKind == MessageKind.QuestionRequest);
        var card = _sut.Messages.Single(m => m.MessageKind == MessageKind.QuestionRequest);
        card.QuestionId.Should().Be("q-1");
        card.QuestionText.Should().Be("Which option?");
        card.QuestionStatus.Should().Be(QuestionStatus.Pending);
    }

    [Fact]
    public async Task QuestionRequestedEvent_WhenEventMatchesCurrentSession_SetsHasPendingQuestionsTrue()
    {
        // Arrange
        var questionEvent = BuildQuestionRequestedEvent();

        // Act
        await TriggerSseEvents(new ChatEvent[] { questionEvent });

        // Assert
        _sut.HasPendingQuestions.Should().BeTrue();
    }

    [Fact]
    public async Task QuestionRequestedEvent_WhenEventHasDifferentSessionId_IsIgnored()
    {
        // Arrange
        var questionEvent = BuildQuestionRequestedEvent(sessionId: "sess-OTHER");

        // Act
        await TriggerSseEvents(new ChatEvent[] { questionEvent });

        // Assert
        _sut.Messages.Should().NotContain(m => m.MessageKind == MessageKind.QuestionRequest);
        _sut.HasPendingQuestions.Should().BeFalse();
    }

    [Fact]
    public async Task QuestionRequestedEvent_WhenEventHasDifferentProjectDirectory_IsIgnored()
    {
        // Arrange
        var questionEvent = BuildQuestionRequestedEvent(projectDirectory: "/other/project");

        // Act
        await TriggerSseEventsWithProjectDirectory("/my/project", new ChatEvent[] { questionEvent });

        // Assert
        _sut.Messages.Should().NotContain(m => m.MessageKind == MessageKind.QuestionRequest);
        _sut.HasPendingQuestions.Should().BeFalse();
    }

    [Fact]
    public async Task QuestionRequestedEvent_WhenDuplicateQuestionIdAlreadyInMessages_IsIgnored()
    {
        // Arrange — inject the same question ID twice in a single SSE stream
        var firstEvent = BuildQuestionRequestedEvent(id: "q-1");
        var duplicateEvent = BuildQuestionRequestedEvent(id: "q-1");

        // Act
        await TriggerSseEvents(new ChatEvent[] { firstEvent, duplicateEvent });

        // Assert — only one card added, not two
        _sut.Messages.Count(m => m.MessageKind == MessageKind.QuestionRequest).Should().Be(1);
    }

    // ─── AnswerQuestionAsync command ──────────────────────────────────────────

    [Fact]
    public async Task AnswerQuestionAsync_WhenApiSucceeds_ResolvesQuestionCard()
    {
        // Arrange
        var questionEvent = BuildQuestionRequestedEvent(id: "q-1");
        await TriggerSseEvents(new ChatEvent[] { questionEvent });

        _apiClient
            .ReplyToQuestionAsync("q-1", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<bool>.Success(true)));

        // Act
        await _sut.AnswerQuestionCommand.ExecuteAsync(new[] { "q-1", "Option A" });

        // Assert
        var card = _sut.Messages.Single(m => m.MessageKind == MessageKind.QuestionRequest);
        card.QuestionStatus.Should().Be(QuestionStatus.Resolved);
        card.ResolvedAnswer.Should().Be("Option A");
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenApiSucceeds_SetsIsAiRespondingTrue()
    {
        // Arrange
        var questionEvent = BuildQuestionRequestedEvent(id: "q-1");
        await TriggerSseEvents(new ChatEvent[] { questionEvent });

        _apiClient
            .ReplyToQuestionAsync("q-1", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<bool>.Success(true)));

        // Act
        await _sut.AnswerQuestionCommand.ExecuteAsync(new[] { "q-1", "Option A" });

        // Assert
        _sut.IsAiResponding.Should().BeTrue();
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenApiSucceeds_DecrementsHasPendingQuestions()
    {
        // Arrange
        var questionEvent = BuildQuestionRequestedEvent(id: "q-1");
        await TriggerSseEvents(new ChatEvent[] { questionEvent });

        _apiClient
            .ReplyToQuestionAsync("q-1", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<bool>.Success(true)));

        // Act
        await _sut.AnswerQuestionCommand.ExecuteAsync(new[] { "q-1", "Option A" });

        // Assert
        _sut.HasPendingQuestions.Should().BeFalse();
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenApiFails_CardRemainsInPendingState()
    {
        // Arrange
        var questionEvent = BuildQuestionRequestedEvent(id: "q-1");
        await TriggerSseEvents(new ChatEvent[] { questionEvent });

        _apiClient
            .ReplyToQuestionAsync("q-1", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<bool>.Failure(
                new OpencodeApiError(ErrorKind.NetworkUnreachable, "offline", null, new HttpRequestException("offline")))));

        // Act
        await _sut.AnswerQuestionCommand.ExecuteAsync(new[] { "q-1", "Option A" });

        // Assert
        var card = _sut.Messages.Single(m => m.MessageKind == MessageKind.QuestionRequest);
        card.QuestionStatus.Should().Be(QuestionStatus.Pending);
        card.ResolvedAnswer.Should().BeNull();
        _sut.HasPendingQuestions.Should().BeTrue();
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenCalledConcurrentlyForSameQuestionId_OnlyFirstCallProceedsToApi()
    {
        // Arrange
        var questionEvent = BuildQuestionRequestedEvent(id: "q-1");
        await TriggerSseEvents(new ChatEvent[] { questionEvent });

        // Use a TaskCompletionSource to hold the first call in-flight while the second arrives
        var tcs = new TaskCompletionSource<OpencodeResult<bool>>();
        _apiClient
            .ReplyToQuestionAsync("q-1", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => tcs.Task);

        // Act — fire both calls concurrently; the second must be dropped by the in-flight guard
        var firstCall = _sut.AnswerQuestionCommand.ExecuteAsync(new[] { "q-1", "Option A" });
        var secondCall = _sut.AnswerQuestionCommand.ExecuteAsync(new[] { "q-1", "Option A" });

        // Release the first call
        tcs.SetResult(OpencodeResult<bool>.Success(true));
        await Task.WhenAll(firstCall, secondCall);

        // Assert — API called exactly once despite two concurrent invocations
        await _apiClient.Received(1).ReplyToQuestionAsync("q-1", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenCalledWithFreeTextAnswer_ResolvesCard()
    {
        // Arrange
        var questionEvent = BuildQuestionRequestedEvent(
            id: "q-1",
            sessionId: "sess-1",
            question: "What is your preference?",
            options: ["Option A", "Option B"],
            allowFreeText: true);
        await TriggerSseEvents(new ChatEvent[] { questionEvent });

        _apiClient
            .ReplyToQuestionAsync("q-1", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<bool>.Success(true)));

        // Act
        await _sut.AnswerQuestionCommand.ExecuteAsync(new[] { "q-1", "My custom answer" });

        // Assert
        var card = _sut.Messages.Single(m => m.MessageKind == MessageKind.QuestionRequest);
        card.QuestionStatus.Should().Be(QuestionStatus.Resolved);
        card.ResolvedAnswer.Should().Be("My custom answer");
        await _apiClient.Received(1).ReplyToQuestionAsync(
            "q-1",
            Arg.Is<IReadOnlyList<string>>(a => a.Count == 1 && a[0] == "My custom answer"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnswerQuestionAsync_WhenApiThrowsException_CardRemainsInPendingStateAndDoesNotCrash()
    {
        // Arrange
        var questionEvent = BuildQuestionRequestedEvent(id: "q-1", sessionId: "sess-1", question: "Test?");
        await TriggerSseEvents(new ChatEvent[] { questionEvent });

        _apiClient
            .ReplyToQuestionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns<Task<OpencodeResult<bool>>>(_ => throw new HttpRequestException("Network error"));

        // Act — must not throw
        await _sut.AnswerQuestionCommand.ExecuteAsync(new[] { "q-1", "Yes" });

        // Assert
        var card = _sut.Messages.Single(m => m.MessageKind == MessageKind.QuestionRequest);
        card.QuestionStatus.Should().Be(QuestionStatus.Pending);
        _sut.HasPendingQuestions.Should().BeTrue();
    }

    // ─── RecoverPendingQuestionsAsync — LoadMessages recovery ────────────────

    /// <summary>
    /// Builds a <see cref="QuestionRequestDto"/> for recovery tests.
    /// </summary>
    private static QuestionRequestDto BuildQuestionRequestDto(
        string id = "q-1",
        string sessionId = "sess-1",
        string question = "Which option?",
        string[]? optionLabels = null,
        bool custom = true,
        QuestionToolRefDto? tool = null)
    {
        var labels = optionLabels ?? new[] { "Option A", "Option B" };
        var options = labels.Select(l => new QuestionOptionDto(l, "")).ToList();
        var questionInfo = new QuestionInfoDto(question, "Test", options, null, custom);
        return new QuestionRequestDto(id, sessionId, new[] { questionInfo }, tool);
    }

    [Fact]
    public async Task LoadMessages_WhenPendingQuestionExistsForCurrentSession_InjectsQuestionCard()
    {
        // Arrange
        var dto = BuildQuestionRequestDto(id: "q-1", sessionId: "sess-1");

        _apiClient
            .GetPendingQuestionsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<IReadOnlyList<QuestionRequestDto>>.Success(
                new List<QuestionRequestDto> { dto })));

        _chatService
            .GetMessagesAsync("sess-1", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Ok(new List<MessageWithPartsDto>()));

        _chatService
            .SubscribeToEventsAsync(Arg.Any<CancellationToken>())
            .Returns(YieldEvents(Array.Empty<ChatEvent>()));

        // Act
        _sut.SetSession("sess-1");
        await Task.Delay(300);

        // Assert
        _sut.Messages.Should().ContainSingle(m => m.MessageKind == MessageKind.QuestionRequest);
        var card = _sut.Messages.Single(m => m.MessageKind == MessageKind.QuestionRequest);
        card.QuestionId.Should().Be("q-1");
        card.QuestionStatus.Should().Be(QuestionStatus.Pending);
        _sut.HasPendingQuestions.Should().BeTrue();
    }

    [Fact]
    public async Task LoadMessages_WhenGetPendingQuestionsReturnsEmptyList_NoQuestionCardInjected()
    {
        // Arrange
        _apiClient
            .GetPendingQuestionsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<IReadOnlyList<QuestionRequestDto>>.Success(
                new List<QuestionRequestDto>())));

        _chatService
            .GetMessagesAsync("sess-1", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Ok(new List<MessageWithPartsDto>()));

        _chatService
            .SubscribeToEventsAsync(Arg.Any<CancellationToken>())
            .Returns(YieldEvents(Array.Empty<ChatEvent>()));

        // Act
        _sut.SetSession("sess-1");
        await Task.Delay(300);

        // Assert
        _sut.Messages.Should().NotContain(m => m.MessageKind == MessageKind.QuestionRequest);
        _sut.HasPendingQuestions.Should().BeFalse();
    }

    [Fact]
    public async Task LoadMessages_WhenGetPendingQuestionsReturnsWrongSessionId_NoQuestionCardInjected()
    {
        // Arrange — DTO belongs to a different session
        var dto = BuildQuestionRequestDto(id: "q-1", sessionId: "sess-OTHER");

        _apiClient
            .GetPendingQuestionsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<IReadOnlyList<QuestionRequestDto>>.Success(
                new List<QuestionRequestDto> { dto })));

        _chatService
            .GetMessagesAsync("sess-1", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Ok(new List<MessageWithPartsDto>()));

        _chatService
            .SubscribeToEventsAsync(Arg.Any<CancellationToken>())
            .Returns(YieldEvents(Array.Empty<ChatEvent>()));

        // Act
        _sut.SetSession("sess-1");
        await Task.Delay(300);

        // Assert
        _sut.Messages.Should().NotContain(m => m.MessageKind == MessageKind.QuestionRequest);
        _sut.HasPendingQuestions.Should().BeFalse();
    }

    [Fact]
    public async Task LoadMessages_WhenGetPendingQuestionsReturnsQuestionWithEmptyQuestionsArray_NoQuestionCardInjected()
    {
        // Arrange — DTO has empty questions array (no actual question content)
        var dto = new QuestionRequestDto("q-1", "sess-1", new List<QuestionInfoDto>());

        _apiClient
            .GetPendingQuestionsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<IReadOnlyList<QuestionRequestDto>>.Success(
                new List<QuestionRequestDto> { dto })));

        _chatService
            .GetMessagesAsync("sess-1", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Ok(new List<MessageWithPartsDto>()));

        _chatService
            .SubscribeToEventsAsync(Arg.Any<CancellationToken>())
            .Returns(YieldEvents(Array.Empty<ChatEvent>()));

        // Act
        _sut.SetSession("sess-1");
        await Task.Delay(300);

        // Assert
        _sut.Messages.Should().NotContain(m => m.MessageKind == MessageKind.QuestionRequest);
        _sut.HasPendingQuestions.Should().BeFalse();
    }

    // ─── HasPendingQuestions counter ──────────────────────────────────────────

    [Fact]
    public async Task AnswerQuestionAsync_WhenLastPendingQuestionAnswered_HasPendingQuestionsIsFalse()
    {
        // Arrange — inject two question cards, then answer both
        var firstEvent = BuildQuestionRequestedEvent(id: "q-1", question: "First question?");
        var secondEvent = BuildQuestionRequestedEvent(id: "q-2", question: "Second question?");
        await TriggerSseEvents(new ChatEvent[] { firstEvent, secondEvent });

        _apiClient
            .ReplyToQuestionAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<bool>.Success(true)));

        // Act — answer both questions
        await _sut.AnswerQuestionCommand.ExecuteAsync(new[] { "q-1", "Answer 1" });
        await _sut.AnswerQuestionCommand.ExecuteAsync(new[] { "q-2", "Answer 2" });

        // Assert — counter reaches zero, flag is false
        _sut.HasPendingQuestions.Should().BeFalse();
    }

    // ─── RecoverPendingQuestionsAsync — multiple questions ──────────────────

    [Fact]
    public async Task RecoverPendingQuestionsAsync_WhenMultiplePendingQuestions_AddsAllCards()
    {
        // Arrange
        var dto1 = BuildQuestionRequestDto(id: "q-1", sessionId: "sess-1", question: "First?");
        var dto2 = BuildQuestionRequestDto(id: "q-2", sessionId: "sess-1", question: "Second?");

        _apiClient
            .GetPendingQuestionsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<IReadOnlyList<QuestionRequestDto>>.Success(
                new List<QuestionRequestDto> { dto1, dto2 })));

        _chatService
            .GetMessagesAsync("sess-1", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Ok(new List<MessageWithPartsDto>()));

        _chatService
            .SubscribeToEventsAsync(Arg.Any<CancellationToken>())
            .Returns(YieldEvents(Array.Empty<ChatEvent>()));

        // Act
        _sut.SetSession("sess-1");
        await Task.Delay(300);

        // Assert
        _sut.Messages.Count(m => m.MessageKind == MessageKind.QuestionRequest).Should().Be(2);
        _sut.HasPendingQuestions.Should().BeTrue();
    }

    [Fact]
    public async Task RecoverPendingQuestionsAsync_WhenQuestionsForDifferentSession_FiltersThemOut()
    {
        // Arrange — all questions belong to a different session
        var dto = BuildQuestionRequestDto(id: "q-1", sessionId: "sess-OTHER", question: "Wrong session?");

        _apiClient
            .GetPendingQuestionsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<IReadOnlyList<QuestionRequestDto>>.Success(
                new List<QuestionRequestDto> { dto })));

        _chatService
            .GetMessagesAsync("sess-1", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Ok(new List<MessageWithPartsDto>()));

        _chatService
            .SubscribeToEventsAsync(Arg.Any<CancellationToken>())
            .Returns(YieldEvents(Array.Empty<ChatEvent>()));

        // Act
        _sut.SetSession("sess-1");
        await Task.Delay(300);

        // Assert
        _sut.Messages.Should().NotContain(m => m.MessageKind == MessageKind.QuestionRequest);
        _sut.HasPendingQuestions.Should().BeFalse();
    }

    [Fact]
    public async Task RecoverPendingQuestionsAsync_WhenDuplicateQuestionAlreadyInMessages_SkipsIt()
    {
        // Arrange — pre-populate Messages with a question card via SSE, then trigger recovery
        var questionEvent = BuildQuestionRequestedEvent(id: "q-1", sessionId: "sess-1");
        var dto = BuildQuestionRequestDto(id: "q-1", sessionId: "sess-1");

        _apiClient
            .GetPendingQuestionsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<IReadOnlyList<QuestionRequestDto>>.Success(
                new List<QuestionRequestDto> { dto })));

        // Act — SSE delivers the question first, then recovery runs
        await TriggerSseEvents(new ChatEvent[] { questionEvent });

        // Assert — only one card, not two
        _sut.Messages.Count(m => m.MessageKind == MessageKind.QuestionRequest).Should().Be(1);
    }

    [Fact]
    public async Task RecoverPendingQuestionsAsync_WhenApiReturnsFailure_CapturesSentryAndContinues()
    {
        // Arrange
        _apiClient
            .GetPendingQuestionsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<IReadOnlyList<QuestionRequestDto>>.Failure(
                new OpencodeApiError(ErrorKind.NetworkUnreachable, "offline", null, new HttpRequestException("offline")))));

        _chatService
            .GetMessagesAsync("sess-1", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Ok(new List<MessageWithPartsDto>()));

        _chatService
            .SubscribeToEventsAsync(Arg.Any<CancellationToken>())
            .Returns(YieldEvents(Array.Empty<ChatEvent>()));

        // Act — must not throw
        _sut.SetSession("sess-1");
        await Task.Delay(300);

        // Assert — no cards added, no crash
        _sut.Messages.Should().NotContain(m => m.MessageKind == MessageKind.QuestionRequest);
        _sut.HasPendingQuestions.Should().BeFalse();
    }

    // ─── Reconnect recovery ──────────────────────────────────────────────────

    [Fact]
    public async Task OnHealthStateChanged_WhenTransitionToHealthy_RecoversPendingQuestions()
    {
        // Arrange — set up the ViewModel with a current session first
        _chatService
            .GetMessagesAsync("sess-1", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Ok(new List<MessageWithPartsDto>()));

        _chatService
            .SubscribeToEventsAsync(Arg.Any<CancellationToken>())
            .Returns(YieldEvents(Array.Empty<ChatEvent>()));

        // Initially return empty so session loads cleanly
        _apiClient
            .GetPendingQuestionsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<IReadOnlyList<QuestionRequestDto>>.Success(
                new List<QuestionRequestDto>())));

        // Also mock GetPendingPermissionsAsync since it's called on Healthy transition
        _apiClient
            .GetPendingPermissionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<IReadOnlyList<PermissionRequestDto>>.Success(
                (IReadOnlyList<PermissionRequestDto>)new List<PermissionRequestDto>())));

        _sut.SetSession("sess-1");
        await Task.Delay(200);

        // Now set up a pending question for the recovery call
        var dto = BuildQuestionRequestDto(id: "q-recover", sessionId: "sess-1", question: "Recovered?");
        _apiClient
            .GetPendingQuestionsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<IReadOnlyList<QuestionRequestDto>>.Success(
                new List<QuestionRequestDto> { dto })));

        // Act — first transition to Degraded to set _previousHealthState, then to Healthy
        _heartbeatMonitor.HealthStateChanged += Raise.Event<Action<ConnectionHealthState>>(ConnectionHealthState.Degraded);
        await Task.Delay(50);
        _heartbeatMonitor.HealthStateChanged += Raise.Event<Action<ConnectionHealthState>>(ConnectionHealthState.Healthy);
        await Task.Delay(300);

        // Assert — the question card should be recovered
        _sut.Messages.Should().Contain(m => m.MessageKind == MessageKind.QuestionRequest && m.QuestionId == "q-recover");
    }

    // ─── Tool call suppression ───────────────────────────────────────────────

    [Fact]
    public async Task HandleMessagePartUpdated_WhenToolNameIsQuestion_AndQuestionCardExists_HidesToolCall()
    {
        // Arrange — load a message, add a question card, then deliver a tool part with toolName "question"
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: ""),
        };

        var questionEvent = BuildQuestionRequestedEvent(id: "q-1", sessionId: "sess-1");

        var stateJson = JsonSerializer.SerializeToElement(new { status = "pending" });
        var toolPart = new PartDto(
            Id: "call-tool-1",
            SessionId: "sess-1",
            MessageId: "msg-1",
            Type: "tool",
            Text: null)
        {
            ToolName = "question",
            State = stateJson,
            CallId = "call-tool-1",
        };
        var toolEvent = new MessagePartUpdatedEvent { Part = toolPart };

        // Act — question card arrives first, then tool part
        await TriggerSseEvents(new ChatEvent[] { questionEvent, toolEvent }, existingMessages);

        // Assert — the tool call should be hidden
        _sut.Messages[0].ToolCalls.Should().HaveCount(1);
        _sut.Messages[0].ToolCalls[0].IsHidden.Should().BeTrue();
    }

    [Fact]
    public async Task HandleQuestionRequested_WhenToolCallCardAlreadyExists_RetroactivelyHidesIt()
    {
        // Arrange — load a message with a tool call card, then deliver a question event with matching ToolCallId
        var existingMessages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "assistant", text: ""),
        };

        var stateJson = JsonSerializer.SerializeToElement(new { status = "pending" });
        var toolPart = new PartDto(
            Id: "call-abc",
            SessionId: "sess-1",
            MessageId: "msg-1",
            Type: "tool",
            Text: null)
        {
            ToolName = "question",
            State = stateJson,
            CallId = "call-abc",
        };
        var toolEvent = new MessagePartUpdatedEvent { Part = toolPart };

        // Question event arrives after the tool call, with matching ToolCallId
        var questionEvent = BuildQuestionRequestedEvent(
            id: "q-1",
            sessionId: "sess-1",
            toolCallId: "call-abc");

        // Act — tool call arrives first, then question event retroactively hides it
        await TriggerSseEvents(new ChatEvent[] { toolEvent, questionEvent }, existingMessages);

        // Assert — the tool call should be retroactively hidden
        _sut.Messages[0].ToolCalls.Should().HaveCount(1);
        _sut.Messages[0].ToolCalls[0].IsHidden.Should().BeTrue();
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}
