using CommunityToolkit.Mvvm.Messaging;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Messages;
using openMob.Core.Models;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for the message composer extensions to <see cref="ChatViewModel"/>.
/// Covers <c>OpenMessageComposerCommand</c>, <c>StreamingStateChangedMessage</c> publishing,
/// and <c>MessageComposedMessage</c> reception.
/// </summary>
[Collection(MessengerTestCollection.Name)]
public sealed class ChatViewModelMessageComposerTests : IDisposable
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

    public ChatViewModelMessageComposerTests()
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

    public void Dispose()
    {
        _sut.Dispose();
    }

    // ─── OpenMessageComposerCommand ──────────────────────────────────────────

    [Fact]
    public async Task OpenMessageComposerCommand_WhenProjectAndSessionSet_CallsShowMessageComposerAsync()
    {
        // Arrange
        _sut.CurrentProjectId = "proj-1";
        _sut.CurrentSessionId = "sess-1";

        // Act
        await _sut.OpenMessageComposerCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowMessageComposerAsync(
            "proj-1", "sess-1", false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenMessageComposerCommand_WhenCurrentProjectIdIsNull_DoesNotCallShowMessageComposerAsync()
    {
        // Arrange
        _sut.CurrentProjectId = null;
        _sut.CurrentSessionId = "sess-1";

        // Act
        await _sut.OpenMessageComposerCommand.ExecuteAsync(null);

        // Assert
        await _popupService.DidNotReceive().ShowMessageComposerAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenMessageComposerCommand_WhenCurrentSessionIdIsNull_DoesNotCallShowMessageComposerAsync()
    {
        // Arrange
        _sut.CurrentProjectId = "proj-1";
        _sut.CurrentSessionId = null;

        // Act
        await _sut.OpenMessageComposerCommand.ExecuteAsync(null);

        // Assert
        await _popupService.DidNotReceive().ShowMessageComposerAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    // ─── OnIsAiRespondingChanged — StreamingStateChangedMessage ──────────────

    [Fact]
    public void OnIsAiRespondingChanged_WhenSetToTrue_SendsStreamingStateChangedMessage()
    {
        // Arrange
        StreamingStateChangedMessage? capturedMessage = null;
        WeakReferenceMessenger.Default.Register<StreamingStateChangedMessage>(this, (_, m) => capturedMessage = m);

        // Act
        _sut.IsAiResponding = true;

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.IsStreaming.Should().BeTrue();

        // Cleanup
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    [Fact]
    public void OnIsAiRespondingChanged_WhenSetToFalse_SendsStreamingStateChangedMessage()
    {
        // Arrange
        _sut.IsAiResponding = true; // set to true first
        StreamingStateChangedMessage? capturedMessage = null;
        WeakReferenceMessenger.Default.Register<StreamingStateChangedMessage>(this, (_, m) => capturedMessage = m);

        // Act
        _sut.IsAiResponding = false;

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.IsStreaming.Should().BeFalse();

        // Cleanup
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    // ─── MessageComposedMessage Reception ────────────────────────────────────

    [Fact]
    public async Task MessageComposedMessage_WhenReceivedForCurrentSession_SetsInputText()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        _sut.CurrentProjectId = "proj-1";

        // Stub SendPromptAsync to prevent actual send logic from failing
        _chatService.SendPromptAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<bool>.Ok(true));

        // Act
        WeakReferenceMessenger.Default.Send(new MessageComposedMessage(
            "proj-1", "sess-1", "Hello from composer", null, null, ThinkingLevel.Medium, false));

        // Allow async handler to complete
        await Task.Delay(100);

        // Assert — InputText should have been set (and then cleared by SendMessageCommand)
        // We verify the send was attempted via the chat service
        await _chatService.Received(1).SendPromptAsync(
            "sess-1",
            "Hello from composer",
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MessageComposedMessage_WhenReceivedForDifferentSession_IsIgnored()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        _sut.CurrentProjectId = "proj-1";

        // Act
        WeakReferenceMessenger.Default.Send(new MessageComposedMessage(
            "proj-1", "sess-OTHER", "Hello", null, null, ThinkingLevel.Medium, false));

        // Allow async handler to complete
        await Task.Delay(100);

        // Assert
        await _chatService.DidNotReceive().SendPromptAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MessageComposedMessage_WhenAgentOverrideDiffersFromSelected_PrependsAgentMention()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        _sut.CurrentProjectId = "proj-1";
        _sut.SelectedAgentName = "default-agent";

        _chatService.SendPromptAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<bool>.Ok(true));

        // Act
        WeakReferenceMessenger.Default.Send(new MessageComposedMessage(
            "proj-1", "sess-1", "Hello", "custom-agent", null, ThinkingLevel.Medium, false));

        // Allow async handler to complete
        await Task.Delay(100);

        // Assert — text should be "@custom-agent Hello"
        await _chatService.Received(1).SendPromptAsync(
            "sess-1",
            "@custom-agent Hello",
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MessageComposedMessage_WhenAgentOverrideMatchesSelected_DoesNotPrependAgentMention()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        _sut.CurrentProjectId = "proj-1";
        _sut.SelectedAgentName = "same-agent";

        _chatService.SendPromptAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<bool>.Ok(true));

        // Act
        WeakReferenceMessenger.Default.Send(new MessageComposedMessage(
            "proj-1", "sess-1", "Hello", "same-agent", null, ThinkingLevel.Medium, false));

        // Allow async handler to complete
        await Task.Delay(100);

        // Assert — text should be "Hello" (no prepend)
        await _chatService.Received(1).SendPromptAsync(
            "sess-1",
            "Hello",
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MessageComposedMessage_WhenAgentOverrideIsNull_DoesNotPrependAgentMention()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        _sut.CurrentProjectId = "proj-1";
        _sut.SelectedAgentName = "some-agent";

        _chatService.SendPromptAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<bool>.Ok(true));

        // Act
        WeakReferenceMessenger.Default.Send(new MessageComposedMessage(
            "proj-1", "sess-1", "Hello", null, null, ThinkingLevel.Medium, false));

        // Allow async handler to complete
        await Task.Delay(100);

        // Assert — text should be "Hello" (no prepend)
        await _chatService.Received(1).SendPromptAsync(
            "sess-1",
            "Hello",
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }
}
