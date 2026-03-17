using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Models;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="ChatViewModel"/>.
/// Covers model selection, preference loading, and more-menu model change behaviour.
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
            _preferenceService);
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
}
