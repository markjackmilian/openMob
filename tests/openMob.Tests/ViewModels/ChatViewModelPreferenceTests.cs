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
/// Unit tests for <see cref="ChatViewModel"/> WeakReferenceMessenger subscription
/// and OpenContextSheetCommand behaviour added in the Session Context Sheet spec.
/// </summary>
public sealed class ChatViewModelPreferenceTests : IDisposable
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

    public ChatViewModelPreferenceTests()
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

    public void Dispose()
    {
        // Unregister the SUT from the messenger to avoid test pollution
        _sut.Dispose();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static ProjectPreference BuildPreference(
        string projectId = "proj-1",
        string? defaultModelId = null)
        => new()
        {
            ProjectId = projectId,
            DefaultModelId = defaultModelId,
            ThinkingLevel = ThinkingLevel.Medium,
        };

    // ─── ProjectPreferenceChangedMessage — matching project ───────────────────

    [Fact]
    public void ProjectPreferenceChangedMessage_WhenProjectIdMatches_UpdatesSelectedModelId()
    {
        // Arrange
        _sut.CurrentProjectId = "proj-1";
        var pref = BuildPreference("proj-1", defaultModelId: "anthropic/claude-sonnet-4-5");
        var message = new ProjectPreferenceChangedMessage("proj-1", pref);

        // Act
        WeakReferenceMessenger.Default.Send(message);

        // Assert
        _sut.SelectedModelId.Should().Be("anthropic/claude-sonnet-4-5");
    }

    [Fact]
    public void ProjectPreferenceChangedMessage_WhenProjectIdMatches_UpdatesSelectedModelName()
    {
        // Arrange
        _sut.CurrentProjectId = "proj-1";
        var pref = BuildPreference("proj-1", defaultModelId: "anthropic/claude-sonnet-4-5");
        var message = new ProjectPreferenceChangedMessage("proj-1", pref);

        // Act
        WeakReferenceMessenger.Default.Send(message);

        // Assert
        _sut.SelectedModelName.Should().Be("claude-sonnet-4-5");
    }

    [Fact]
    public void ProjectPreferenceChangedMessage_WhenProjectIdMatches_AndModelIdIsNull_ClearsSelectedModelId()
    {
        // Arrange
        _sut.CurrentProjectId = "proj-1";
        _sut.SelectedModelId = "anthropic/old-model";
        var pref = BuildPreference("proj-1", defaultModelId: null);
        var message = new ProjectPreferenceChangedMessage("proj-1", pref);

        // Act
        WeakReferenceMessenger.Default.Send(message);

        // Assert
        _sut.SelectedModelId.Should().BeNull();
    }

    [Fact]
    public void ProjectPreferenceChangedMessage_WhenProjectIdMatches_AndModelIdIsNull_ClearsSelectedModelName()
    {
        // Arrange
        _sut.CurrentProjectId = "proj-1";
        _sut.SelectedModelName = "old-model";
        var pref = BuildPreference("proj-1", defaultModelId: null);
        var message = new ProjectPreferenceChangedMessage("proj-1", pref);

        // Act
        WeakReferenceMessenger.Default.Send(message);

        // Assert
        _sut.SelectedModelName.Should().BeNull();
    }

    // ─── ProjectPreferenceChangedMessage — non-matching project ───────────────

    [Fact]
    public void ProjectPreferenceChangedMessage_WhenProjectIdDoesNotMatch_DoesNotUpdate()
    {
        // Arrange
        _sut.CurrentProjectId = "proj-1";
        _sut.SelectedModelId = "anthropic/old-model";
        var pref = BuildPreference("proj-OTHER", defaultModelId: "openai/gpt-4o");
        var message = new ProjectPreferenceChangedMessage("proj-OTHER", pref);

        // Act
        WeakReferenceMessenger.Default.Send(message);

        // Assert
        _sut.SelectedModelId.Should().Be("anthropic/old-model");
    }

    // ─── OpenContextSheetCommand ──────────────────────────────────────────────

    [Fact]
    public async Task OpenContextSheetCommand_WhenCurrentProjectIdIsNull_DoesNotCallPopupService()
    {
        // Arrange — CurrentProjectId is null by default

        // Act
        await _sut.OpenContextSheetCommand.ExecuteAsync(null);

        // Assert
        await _popupService.DidNotReceive().ShowContextSheetAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenContextSheetCommand_WhenCurrentProjectIdIsSet_CallsShowContextSheetAsyncWithCorrectArgs()
    {
        // Arrange
        _sut.CurrentProjectId = "proj-1";
        _sut.CurrentSessionId = "sess-1";
        _popupService.ShowContextSheetAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.OpenContextSheetCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowContextSheetAsync(
            "proj-1",
            "sess-1",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenContextSheetCommand_WhenCurrentSessionIdIsNull_CallsShowContextSheetAsyncWithEmptySessionId()
    {
        // Arrange
        _sut.CurrentProjectId = "proj-1";
        // CurrentSessionId is null by default
        _popupService.ShowContextSheetAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.OpenContextSheetCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowContextSheetAsync(
            "proj-1",
            string.Empty,
            Arg.Any<CancellationToken>());
    }
}
