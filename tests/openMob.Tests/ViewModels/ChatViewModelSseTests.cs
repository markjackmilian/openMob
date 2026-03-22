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
[Collection(MessengerTestCollection.Name)]
public sealed class ChatViewModelSseTests : IDisposable
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
    private readonly IActiveProjectService _activeProjectService;
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
        _activeProjectService = Substitute.For<IActiveProjectService>();

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
            _dispatcher,
            _activeProjectService);
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

    public void Dispose()
    {
        _sut.Dispose();
    }
}
