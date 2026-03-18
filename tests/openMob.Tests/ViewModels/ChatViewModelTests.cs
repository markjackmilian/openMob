using System.Text.Json;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Models;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="ChatViewModel"/>.
/// Covers model selection, preference loading, more-menu model change behaviour,
/// conversation loop (message loading, sending, cancellation, grouping), and disposal.
/// </summary>
public sealed class ChatViewModelTests
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

    public ChatViewModelTests()
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

    private static ProjectDto BuildProject(string id = "proj-1", string worktree = "/home/user/myproject", string? vcs = "git")
    {
        var time = new ProjectTimeDto(Created: 1710000000000, Initialized: null);
        return new ProjectDto(Id: id, Worktree: worktree, VcsDir: null, Vcs: vcs, Time: time);
    }

    private static openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto BuildSession(
        string id = "sess-1",
        string projectId = "proj-1",
        string title = "Test Session",
        long updated = 1710000001000)
    {
        var time = new SessionTimeDto(Created: 1710000000000, Updated: updated, Compacting: null);
        return new openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto(
            Id: id, ProjectId: projectId, Directory: "/path", ParentId: null,
            Summary: null, Share: null, Title: title, Version: "1",
            Time: time, Revert: null);
    }

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
        var textPayload = JsonSerializer.SerializeToElement(new { type = "text", text });

        var info = new MessageInfoDto(Id: id, SessionId: sessionId, Role: role, Time: timeJson);
        var part = new PartDto(Id: $"part-{id}", SessionId: sessionId, MessageId: id, Type: "text", Payload: textPayload);
        return new MessageWithPartsDto(Info: info, Parts: new[] { part });
    }

    // ─── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Assert
        _sut.ProjectName.Should().Be("No project");
        _sut.SessionName.Should().Be("New chat");
        _sut.CurrentSessionId.Should().BeNull();
        _sut.CurrentProjectId.Should().BeNull();
        _sut.SelectedModelId.Should().BeNull();
        _sut.SelectedModelName.Should().BeNull();
    }

    [Fact]
    public void SelectedModelName_DefaultsToNull_ForPlaceholderDisplay()
    {
        // Assert
        _sut.SelectedModelName.Should().BeNull();
    }

    [Fact]
    public void Constructor_PopulatesFourSuggestionChips()
    {
        // Assert
        _sut.SuggestionChips.Should().HaveCount(4);
    }

    // ─── LoadContextCommand — model preference loading ────────────────────────

    [Fact]
    public async Task LoadContextCommand_WhenPreferenceHasDefaultModelId_SetsSelectedModelId()
    {
        // Arrange
        var project = BuildProject("proj-1");
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>()).Returns(project);
        _preferenceService.GetAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(new ProjectPreference { ProjectId = "proj-1", DefaultModelId = "anthropic/claude-sonnet-4-5" });

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.SelectedModelId.Should().Be("anthropic/claude-sonnet-4-5");
    }

    [Fact]
    public async Task LoadContextCommand_WhenPreferenceHasDefaultModelId_SetsSelectedModelName()
    {
        // Arrange
        var project = BuildProject("proj-1");
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>()).Returns(project);
        _preferenceService.GetAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(new ProjectPreference { ProjectId = "proj-1", DefaultModelId = "anthropic/claude-sonnet-4-5" });

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.SelectedModelName.Should().Be("claude-sonnet-4-5");
    }

    [Fact]
    public async Task LoadContextCommand_WhenNoPreferenceExists_SelectedModelIdRemainsNull()
    {
        // Arrange
        var project = BuildProject("proj-1");
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>()).Returns(project);
        _preferenceService.GetAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns((ProjectPreference?)null);

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.SelectedModelId.Should().BeNull();
    }

    [Fact]
    public async Task LoadContextCommand_WhenNoPreferenceExists_SelectedModelNameRemainsNull()
    {
        // Arrange
        var project = BuildProject("proj-1");
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>()).Returns(project);
        _preferenceService.GetAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns((ProjectPreference?)null);

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.SelectedModelName.Should().BeNull();
    }

    [Theory]
    [InlineData("openai/gpt-4", "gpt-4")]
    [InlineData("anthropic/claude-3-opus", "claude-3-opus")]
    [InlineData("google/gemini-1.5-pro", "gemini-1.5-pro")]
    [InlineData("model-without-slash", "model-without-slash")]
    public async Task LoadContextCommand_ExtractsModelNameFromFullModelId(
        string fullModelId, string expectedModelName)
    {
        // Arrange
        var project = BuildProject("proj-1");
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>()).Returns(project);
        _preferenceService.GetAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(new ProjectPreference { ProjectId = "proj-1", DefaultModelId = fullModelId });

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.SelectedModelName.Should().Be(expectedModelName);
    }

    [Fact]
    public async Task LoadContextCommand_WhenNoCurrentProject_SelectedModelIdRemainsNull()
    {
        // Arrange
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.SelectedModelId.Should().BeNull();
        _sut.SelectedModelName.Should().BeNull();
    }

    [Fact]
    public async Task LoadContextCommand_WhenPreferenceDefaultModelIdIsNull_SelectedModelIdRemainsNull()
    {
        // Arrange
        var project = BuildProject("proj-1");
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>()).Returns(project);
        _preferenceService.GetAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(new ProjectPreference { ProjectId = "proj-1", DefaultModelId = null });

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.SelectedModelId.Should().BeNull();
        _sut.SelectedModelName.Should().BeNull();
    }

    // ─── LoadContextCommand — project and session loading ─────────────────────

    [Fact]
    public async Task LoadContextCommand_WhenProjectExists_SetsProjectName()
    {
        // Arrange
        var project = BuildProject("proj-1", "/home/user/myproject");
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>()).Returns(project);

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.ProjectName.Should().Be("myproject");
        _sut.CurrentProjectId.Should().Be("proj-1");
    }

    [Fact]
    public async Task LoadContextCommand_WhenNoProject_SetsProjectNameToDefault()
    {
        // Arrange
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.ProjectName.Should().Be("No project");
        _sut.CurrentProjectId.Should().BeNull();
    }

    // ─── ShowMoreMenuCommand — Change model ───────────────────────────────────

    [Fact]
    public async Task ShowMoreMenuCommand_WhenChangeModelSelected_UpdatesSelectedModelId()
    {
        // Arrange
        _popupService.ShowOptionSheetAsync("More", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns("Change model");
        _popupService.ShowModelPickerAsync(Arg.Any<Action<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var callback = callInfo.Arg<Action<string>>();
                callback("anthropic/claude-sonnet-4-5");
                return Task.CompletedTask;
            });

        // Act
        await _sut.ShowMoreMenuCommand.ExecuteAsync(null);

        // Assert
        _sut.SelectedModelId.Should().Be("anthropic/claude-sonnet-4-5");
    }

    [Fact]
    public async Task ShowMoreMenuCommand_WhenChangeModelSelected_UpdatesSelectedModelName()
    {
        // Arrange
        _popupService.ShowOptionSheetAsync("More", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns("Change model");
        _popupService.ShowModelPickerAsync(Arg.Any<Action<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var callback = callInfo.Arg<Action<string>>();
                callback("openai/gpt-4o");
                return Task.CompletedTask;
            });

        // Act
        await _sut.ShowMoreMenuCommand.ExecuteAsync(null);

        // Assert
        _sut.SelectedModelName.Should().Be("gpt-4o");
    }

    [Fact]
    public async Task ShowMoreMenuCommand_WhenChangeModelSelected_DoesNotCallSetDefaultModelAsync()
    {
        // Arrange
        _popupService.ShowOptionSheetAsync("More", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns("Change model");
        _popupService.ShowModelPickerAsync(Arg.Any<Action<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var callback = callInfo.Arg<Action<string>>();
                callback("anthropic/claude-sonnet-4-5");
                return Task.CompletedTask;
            });

        // Act
        await _sut.ShowMoreMenuCommand.ExecuteAsync(null);

        // Assert
        await _preferenceService.DidNotReceive().SetDefaultModelAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShowMoreMenuCommand_WhenDismissed_DoesNotChangeSelectedModelId()
    {
        // Arrange
        _sut.SelectedModelId = "original/model";
        _popupService.ShowOptionSheetAsync("More", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        await _sut.ShowMoreMenuCommand.ExecuteAsync(null);

        // Assert
        _sut.SelectedModelId.Should().Be("original/model");
    }

    // ─── SelectedModelId format validation ────────────────────────────────────

    [Theory]
    [InlineData("anthropic/claude-sonnet-4-5")]
    [InlineData("openai/gpt-4")]
    [InlineData("google/gemini-pro")]
    public void SelectedModelId_WhenSetWithProviderSlashModel_SplitsIntoTwoNonEmptyParts(string modelId)
    {
        // Act
        _sut.SelectedModelId = modelId;

        // Assert
        var parts = _sut.SelectedModelId!.Split('/', 2);
        parts.Should().HaveCount(2);
        parts[0].Should().NotBeNullOrEmpty();
        parts[1].Should().NotBeNullOrEmpty();
    }

    // ─── NewChatCommand ───────────────────────────────────────────────────────

    [Fact]
    public async Task NewChatCommand_WhenSessionCreated_UpdatesCurrentSessionId()
    {
        // Arrange
        var session = BuildSession("new-sess", title: "New Chat");
        _sessionService.CreateSessionAsync(null, Arg.Any<CancellationToken>()).Returns(session);

        // Act
        await _sut.NewChatCommand.ExecuteAsync(null);

        // Assert
        _sut.CurrentSessionId.Should().Be("new-sess");
        _sut.SessionName.Should().Be("New Chat");
    }

    [Fact]
    public async Task NewChatCommand_WhenSessionCreationFails_ShowsError()
    {
        // Arrange
        _sessionService.CreateSessionAsync(null, Arg.Any<CancellationToken>())
            .Returns((openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto?)null);

        // Act
        await _sut.NewChatCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowErrorAsync(
            "Error",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    // ─── Status banner ────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadContextCommand_WhenServerOffline_SetsStatusBannerToServerOffline()
    {
        // Arrange
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);
        _connectionManager.ConnectionStatus.Returns(ServerConnectionStatus.Disconnected);

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.StatusBanner.Should().NotBeNull();
        _sut.StatusBanner!.Type.Should().Be(StatusBannerType.ServerOffline);
    }

    [Fact]
    public async Task LoadContextCommand_WhenNoProviderConfigured_SetsStatusBannerToNoProvider()
    {
        // Arrange
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);
        _connectionManager.ConnectionStatus.Returns(ServerConnectionStatus.Connected);
        _providerService.HasAnyProviderConfiguredAsync(Arg.Any<CancellationToken>()).Returns(false);

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.StatusBanner.Should().NotBeNull();
        _sut.StatusBanner!.Type.Should().Be(StatusBannerType.NoProvider);
    }

    [Fact]
    public async Task LoadContextCommand_WhenServerConnectedAndProviderConfigured_StatusBannerIsNull()
    {
        // Arrange
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);
        _connectionManager.ConnectionStatus.Returns(ServerConnectionStatus.Connected);
        _providerService.HasAnyProviderConfiguredAsync(Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.StatusBanner.Should().BeNull();
    }

    // ─── SetSession ───────────────────────────────────────────────────────────

    [Fact]
    public void SetSession_UpdatesCurrentSessionId()
    {
        // Act
        _sut.SetSession("sess-42");

        // Assert
        _sut.CurrentSessionId.Should().Be("sess-42");
    }

    [Fact]
    public void SetSession_ClearsMessages()
    {
        // Arrange — pre-populate messages by setting a session and loading data
        _sut.Messages.Add(ChatMessage.CreateOptimistic("sess-old", "old message"));

        // Act
        _sut.SetSession("sess-new");

        // Assert
        _sut.Messages.Should().BeEmpty();
    }

    [Fact]
    public void SetSession_SameSessionId_DoesNotReload()
    {
        // Arrange — set session once
        _sut.SetSession("sess-1");
        _chatService.ClearReceivedCalls();

        // Act — set same session again
        _sut.SetSession("sess-1");

        // Assert — GetMessagesAsync should not be called again (LoadMessagesCommand not re-invoked)
        _chatService.DidNotReceive().GetMessagesAsync(
            Arg.Any<string>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>());
    }

    // ─── LoadMessagesCommand ──────────────────────────────────────────────────

    [Fact]
    public async Task LoadMessagesCommand_WhenServiceReturnsMessages_PopulatesCollection()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        var messages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "user", text: "Hello"),
            BuildMessageDto(id: "msg-2", sessionId: "sess-1", role: "assistant", text: "Hi there", completed: true),
        };
        _chatService.GetMessagesAsync("sess-1", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Ok(messages));

        // Act
        await _sut.LoadMessagesCommand.ExecuteAsync(null);

        // Assert
        _sut.Messages.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadMessagesCommand_WhenServiceFails_SetsErrorMessage()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        var error = new ChatServiceError(ChatServiceErrorKind.NetworkError, "Connection refused");
        _chatService.GetMessagesAsync("sess-1", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Fail(error));

        // Act
        await _sut.LoadMessagesCommand.ExecuteAsync(null);

        // Assert
        _sut.ErrorMessage.Should().NotBeNullOrEmpty();
        _sut.ErrorMessage.Should().Contain("Network error");
    }

    [Fact]
    public async Task LoadMessagesCommand_SetsIsBusyDuringExecution()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        var isBusyDuringCall = false;
        _chatService.GetMessagesAsync("sess-1", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                isBusyDuringCall = _sut.IsBusy;
                return ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Ok(
                    Array.Empty<MessageWithPartsDto>());
            });

        // Act
        await _sut.LoadMessagesCommand.ExecuteAsync(null);

        // Assert
        isBusyDuringCall.Should().BeTrue();
        _sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task LoadMessagesCommand_WithNullSessionId_DoesNotCallService()
    {
        // Arrange — CurrentSessionId is null by default

        // Act
        await _sut.LoadMessagesCommand.ExecuteAsync(null);

        // Assert
        await _chatService.DidNotReceive().GetMessagesAsync(
            Arg.Any<string>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>());
    }

    // ─── SendMessageCommand ───────────────────────────────────────────────────

    [Fact]
    public async Task SendMessageCommand_ClearsInputAndAddsOptimisticMessage()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        _sut.InputText = "Hello AI";
        _chatService.SendPromptAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<bool>.Ok(true));

        // Act
        await _sut.SendMessageCommand.ExecuteAsync(null);

        // Assert
        _sut.InputText.Should().BeEmpty();
        _sut.Messages.Should().ContainSingle();
        _sut.Messages[0].TextContent.Should().Be("Hello AI");
        _sut.Messages[0].IsFromUser.Should().BeTrue();
    }

    [Fact]
    public async Task SendMessageCommand_OnSuccess_SetsDeliveryStatusToSent()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        _sut.InputText = "Hello AI";
        _chatService.SendPromptAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<bool>.Ok(true));

        // Act
        await _sut.SendMessageCommand.ExecuteAsync(null);

        // Assert
        _sut.Messages[0].DeliveryStatus.Should().Be(MessageDeliveryStatus.Sent);
    }

    [Fact]
    public async Task SendMessageCommand_OnError_SetsDeliveryStatusToError()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        _sut.InputText = "Hello AI";
        var error = new ChatServiceError(ChatServiceErrorKind.ServerError, "Internal server error");
        _chatService.SendPromptAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<bool>.Fail(error));

        // Act
        await _sut.SendMessageCommand.ExecuteAsync(null);

        // Assert
        _sut.Messages[0].DeliveryStatus.Should().Be(MessageDeliveryStatus.Error);
    }

    [Fact]
    public void SendMessageCommand_CannotExecuteWhenInputEmpty()
    {
        // Arrange
        _sut.InputText = "";

        // Act
        var canExecute = _sut.SendMessageCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeFalse();
    }

    [Fact]
    public void SendMessageCommand_CannotExecuteWhenAiResponding()
    {
        // Arrange
        _sut.InputText = "Hello";
        _sut.IsAiResponding = true;

        // Act
        var canExecute = _sut.SendMessageCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeFalse();
    }

    [Fact]
    public async Task SendMessageCommand_SetsIsAiRespondingTrue()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        _sut.InputText = "Hello AI";
        var isAiRespondingDuringCall = false;
        _chatService.SendPromptAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                isAiRespondingDuringCall = _sut.IsAiResponding;
                return ChatServiceResult<bool>.Ok(true);
            });

        // Act
        await _sut.SendMessageCommand.ExecuteAsync(null);

        // Assert
        isAiRespondingDuringCall.Should().BeTrue();
    }

    // ─── CancelResponseCommand ────────────────────────────────────────────────

    [Fact]
    public async Task CancelResponseCommand_CallsAbortSessionAsync()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        _sut.IsAiResponding = true;
        _apiClient.AbortSessionAsync("sess-1", Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<bool>.Success(true));

        // Act
        await _sut.CancelResponseCommand.ExecuteAsync(null);

        // Assert
        await _apiClient.Received(1).AbortSessionAsync("sess-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelResponseCommand_SetsIsAiRespondingFalse()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        _sut.IsAiResponding = true;
        _apiClient.AbortSessionAsync("sess-1", Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<bool>.Success(true));

        // Act
        await _sut.CancelResponseCommand.ExecuteAsync(null);

        // Assert
        _sut.IsAiResponding.Should().BeFalse();
    }

    // ─── DismissErrorCommand ──────────────────────────────────────────────────

    [Fact]
    public void DismissErrorCommand_ClearsErrorMessage()
    {
        // Arrange
        _sut.ErrorMessage = "Something went wrong";

        // Act
        _sut.DismissErrorCommand.Execute(null);

        // Assert
        _sut.ErrorMessage.Should().BeNull();
    }

    // ─── Grouping ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Grouping_ConsecutiveSameSender_FirstAndLastCorrect()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        var messages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "user", text: "Hello"),
            BuildMessageDto(id: "msg-2", sessionId: "sess-1", role: "user", text: "How are you?"),
            BuildMessageDto(id: "msg-3", sessionId: "sess-1", role: "user", text: "Anyone there?"),
        };
        _chatService.GetMessagesAsync("sess-1", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Ok(messages));

        // Act
        await _sut.LoadMessagesCommand.ExecuteAsync(null);

        // Assert
        _sut.Messages[0].IsFirstInGroup.Should().BeTrue();
        _sut.Messages[0].IsLastInGroup.Should().BeFalse();
        _sut.Messages[1].IsFirstInGroup.Should().BeFalse();
        _sut.Messages[1].IsLastInGroup.Should().BeFalse();
        _sut.Messages[2].IsFirstInGroup.Should().BeFalse();
        _sut.Messages[2].IsLastInGroup.Should().BeTrue();
    }

    [Fact]
    public async Task Grouping_AlternatingSenders_EachIsFirstAndLastInGroup()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        var messages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "user", text: "Hello"),
            BuildMessageDto(id: "msg-2", sessionId: "sess-1", role: "assistant", text: "Hi", completed: true),
            BuildMessageDto(id: "msg-3", sessionId: "sess-1", role: "user", text: "Thanks"),
        };
        _chatService.GetMessagesAsync("sess-1", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Ok(messages));

        // Act
        await _sut.LoadMessagesCommand.ExecuteAsync(null);

        // Assert
        _sut.Messages[0].IsFirstInGroup.Should().BeTrue();
        _sut.Messages[0].IsLastInGroup.Should().BeTrue();
        _sut.Messages[1].IsFirstInGroup.Should().BeTrue();
        _sut.Messages[1].IsLastInGroup.Should().BeTrue();
        _sut.Messages[2].IsFirstInGroup.Should().BeTrue();
        _sut.Messages[2].IsLastInGroup.Should().BeTrue();
    }

    // ─── HasError / IsEmpty ───────────────────────────────────────────────────

    [Fact]
    public void HasError_WhenErrorMessageSet_ReturnsTrue()
    {
        // Act
        _sut.ErrorMessage = "Something went wrong";

        // Assert
        _sut.HasError.Should().BeTrue();
    }

    [Fact]
    public async Task IsEmpty_WhenMessagesLoaded_ReturnsFalse()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        var messages = new List<MessageWithPartsDto>
        {
            BuildMessageDto(id: "msg-1", sessionId: "sess-1", role: "user", text: "Hello"),
        };
        _chatService.GetMessagesAsync("sess-1", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Ok(messages));

        // Act
        await _sut.LoadMessagesCommand.ExecuteAsync(null);

        // Assert
        _sut.IsEmpty.Should().BeFalse();
    }

    // ─── Dispose ──────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_ClearsMessagesAndSuggestionChips()
    {
        // Arrange — add some data
        _sut.Messages.Add(ChatMessage.CreateOptimistic("sess-1", "test"));

        // Act
        _sut.Dispose();

        // Assert
        _sut.Messages.Should().BeEmpty();
        _sut.SuggestionChips.Should().BeEmpty();
    }
}
