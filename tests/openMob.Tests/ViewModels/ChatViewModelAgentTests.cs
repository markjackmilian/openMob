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
/// Unit tests for <see cref="ChatViewModel"/> agent selection behaviour added in
/// the Session Context Sheet spec (2of3): <see cref="ChatViewModel.SelectedAgentName"/>,
/// <see cref="ChatViewModel.SelectedAgentDisplayName"/>, agent loading via
/// <see cref="ChatViewModel.LoadContextCommand"/>, and
/// <see cref="ProjectPreferenceChangedMessage"/> handling for agent updates.
/// </summary>
[Collection(MessengerTestCollection.Name)]
public sealed class ChatViewModelAgentTests : IDisposable
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
    private readonly ChatViewModel _sut;

    public ChatViewModelAgentTests()
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
            Substitute.For<IHeartbeatMonitorService>());
    }

    public void Dispose()
    {
        // Unregister the SUT from the messenger to avoid test pollution
        _sut.Dispose();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static ProjectDto BuildProject(string id = "proj-1", string worktree = "/home/user/myproject")
    {
        var time = new ProjectTimeDto(Created: 1710000000000, Initialized: null);
        return new ProjectDto(Id: id, Worktree: worktree, VcsDir: null, Vcs: "git", Time: time);
    }

    private static ProjectPreference BuildPreference(
        string projectId = "proj-1",
        string? agentName = null,
        string? defaultModelId = null)
        => new()
        {
            ProjectId = projectId,
            AgentName = agentName,
            DefaultModelId = defaultModelId,
            ThinkingLevel = ThinkingLevel.Medium,
        };

    // ─── SelectedAgentDisplayName — computed property ─────────────────────────

    [Theory]
    [InlineData(null, "build")]
    [InlineData("coder", "coder")]
    public void SelectedAgentDisplayName_ReturnsExpectedValue(string? agentName, string expectedDisplay)
    {
        // Arrange
        _sut.SelectedAgentName = agentName;

        // Act
        var result = _sut.SelectedAgentDisplayName;

        // Assert
        result.Should().Be(expectedDisplay);
    }

    // ─── LoadContextCommand — agent preference loading ────────────────────────

    [Fact]
    public async Task LoadContextCommand_WhenProjectHasAgentPreference_PopulatesSelectedAgentName()
    {
        // Arrange
        var project = BuildProject("proj-1");
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>()).Returns(project);
        _preferenceService.GetAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference("proj-1", agentName: "coder"));

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.SelectedAgentName.Should().Be("coder");
    }

    [Fact]
    public async Task LoadContextCommand_WhenProjectHasNoPreference_SelectedAgentNameIsNull()
    {
        // Arrange
        var project = BuildProject("proj-1");
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>()).Returns(project);
        _preferenceService.GetAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns((ProjectPreference?)null);

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.SelectedAgentName.Should().BeNull();
    }

    [Fact]
    public async Task LoadContextCommand_WhenPreferenceHasNullAgentName_SelectedAgentNameIsNull()
    {
        // Arrange
        var project = BuildProject("proj-1");
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>()).Returns(project);
        _preferenceService.GetAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference("proj-1", agentName: null));

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.SelectedAgentName.Should().BeNull();
    }

    [Fact]
    public async Task LoadContextCommand_WhenPreferenceHasNullAgentName_SelectedAgentDisplayNameIsBuild()
    {
        // Arrange
        var project = BuildProject("proj-1");
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>()).Returns(project);
        _preferenceService.GetAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference("proj-1", agentName: null));

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.SelectedAgentDisplayName.Should().Be("build");
    }

    // ─── ProjectPreferenceChangedMessage — agent updates ─────────────────────
    // Use a unique project ID ("agent-proj-A") to avoid cross-test-class interference
    // with ChatViewModelPreferenceTests which also uses WeakReferenceMessenger.Default
    // and may run in parallel (xUnit parallelises test classes by default).

    [Fact]
    public void ProjectPreferenceChangedMessage_WhenAgentNameChanges_UpdatesSelectedAgentName()
    {
        // Arrange
        _sut.CurrentProjectId = "agent-proj-A";
        var pref = BuildPreference("agent-proj-A", agentName: "researcher");
        var message = new ProjectPreferenceChangedMessage("agent-proj-A", pref);

        // Act
        WeakReferenceMessenger.Default.Send(message);

        // Assert
        _sut.SelectedAgentName.Should().Be("researcher");
    }

    [Fact]
    public void ProjectPreferenceChangedMessage_WhenAgentNameChanges_UpdatesSelectedAgentDisplayName()
    {
        // Arrange
        _sut.CurrentProjectId = "agent-proj-A";
        var pref = BuildPreference("agent-proj-A", agentName: "researcher");
        var message = new ProjectPreferenceChangedMessage("agent-proj-A", pref);

        // Act
        WeakReferenceMessenger.Default.Send(message);

        // Assert
        _sut.SelectedAgentDisplayName.Should().Be("researcher");
    }

    [Fact]
    public void ProjectPreferenceChangedMessage_WhenAgentNameIsNull_SelectedAgentNameIsNull()
    {
        // Arrange
        _sut.CurrentProjectId = "agent-proj-A";
        _sut.SelectedAgentName = "coder";
        var pref = BuildPreference("agent-proj-A", agentName: null);
        var message = new ProjectPreferenceChangedMessage("agent-proj-A", pref);

        // Act
        WeakReferenceMessenger.Default.Send(message);

        // Assert
        _sut.SelectedAgentName.Should().BeNull();
    }

    [Fact]
    public void ProjectPreferenceChangedMessage_WhenAgentNameIsNull_SelectedAgentDisplayNameIsBuild()
    {
        // Arrange
        _sut.CurrentProjectId = "agent-proj-A";
        _sut.SelectedAgentName = "coder";
        var pref = BuildPreference("agent-proj-A", agentName: null);
        var message = new ProjectPreferenceChangedMessage("agent-proj-A", pref);

        // Act
        WeakReferenceMessenger.Default.Send(message);

        // Assert
        _sut.SelectedAgentDisplayName.Should().Be("build");
    }

    [Fact]
    public void ProjectPreferenceChangedMessage_WhenProjectIdDoesNotMatch_DoesNotUpdateAgentName()
    {
        // Arrange
        _sut.CurrentProjectId = "agent-proj-A";
        _sut.SelectedAgentName = "coder";
        var pref = BuildPreference("agent-proj-B", agentName: "researcher");
        var message = new ProjectPreferenceChangedMessage("agent-proj-B", pref);

        // Act
        WeakReferenceMessenger.Default.Send(message);

        // Assert
        _sut.SelectedAgentName.Should().Be("coder");
    }
}
