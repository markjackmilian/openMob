using CommunityToolkit.Mvvm.Messaging;
using NSubstitute.ExceptionExtensions;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Messages;
using openMob.Core.Models;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="ChatViewModel"/> handling of <see cref="ActiveProjectChangedMessage"/>.
/// Covers REQ-009: ChatViewModel subscribes to ActiveProjectChangedMessage and navigates
/// to the most recent session of the new active project, or to new chat if none exist.
/// </summary>
public sealed class ChatViewModelProjectChangeTests : IDisposable
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

    public ChatViewModelProjectChangeTests()
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

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static ProjectDto BuildProject(string id = "proj-1", string worktree = "/home/user/myproject")
    {
        var time = new ProjectTimeDto(Created: 1710000000000, Initialized: null);
        return new ProjectDto(Id: id, Worktree: worktree, VcsDir: null, Vcs: "git", Time: time);
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
    /// Publishes an <see cref="ActiveProjectChangedMessage"/> and waits briefly
    /// for the async handler to complete.
    /// </summary>
    private async Task PublishProjectChangedAndWaitAsync(ProjectDto project)
    {
        WeakReferenceMessenger.Default.Send(new ActiveProjectChangedMessage(project));
        await Task.Delay(200);
    }

    // ─── ActiveProjectChangedMessage — session exists [REQ-009] ──────────────

    [Fact]
    public async Task ActiveProjectChanged_WhenProjectHasSessions_NavigatesToMostRecentSession()
    {
        // Arrange
        var newProject = BuildProject("proj-new", "/home/user/newproject");
        var lastSession = BuildSession("sess-latest", "proj-new", "Latest Session");
        _sessionService.GetLastSessionForProjectAsync("proj-new", Arg.Any<CancellationToken>())
            .Returns(lastSession);

        // Act
        await PublishProjectChangedAndWaitAsync(newProject);

        // Assert
        await _navigationService.Received(1).GoToAsync(
            "//chat",
            Arg.Is<IDictionary<string, object>>(d => (string)d["sessionId"] == "sess-latest"),
            Arg.Any<CancellationToken>());
    }

    // ─── ActiveProjectChangedMessage — no sessions [REQ-009] ─────────────────

    [Fact]
    public async Task ActiveProjectChanged_WhenProjectHasNoSessions_NavigatesToNewChat()
    {
        // Arrange
        var newProject = BuildProject("proj-empty", "/home/user/emptyproject");
        _sessionService.GetLastSessionForProjectAsync("proj-empty", Arg.Any<CancellationToken>())
            .Returns((openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto?)null);

        // Act
        await PublishProjectChangedAndWaitAsync(newProject);

        // Assert — should navigate to chat without a session (new chat)
        await _navigationService.Received(1).GoToAsync(
            "//chat",
            Arg.Any<CancellationToken>());
    }

    // ─── ActiveProjectChangedMessage — error handling ────────────────────────

    [Fact]
    public async Task ActiveProjectChanged_WhenServiceThrows_DoesNotCrash()
    {
        // Arrange
        var newProject = BuildProject("proj-error", "/home/user/errorproject");
        _sessionService.GetLastSessionForProjectAsync("proj-error", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Service failure"));

        // Act — should not throw
        var act = async () => await PublishProjectChangedAndWaitAsync(newProject);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ActiveProjectChanged_WhenServiceThrows_DoesNotNavigate()
    {
        // Arrange
        var newProject = BuildProject("proj-error", "/home/user/errorproject");
        _sessionService.GetLastSessionForProjectAsync("proj-error", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Service failure"));

        // Act
        await PublishProjectChangedAndWaitAsync(newProject);

        // Assert — no navigation should have occurred
        await _navigationService.DidNotReceive().GoToAsync(
            Arg.Any<string>(),
            Arg.Any<IDictionary<string, object>>(),
            Arg.Any<CancellationToken>());
        await _navigationService.DidNotReceive().GoToAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    // ─── ActiveProjectChangedMessage — calls correct service method ──────────

    [Fact]
    public async Task ActiveProjectChanged_CallsGetLastSessionForProjectAsyncWithCorrectProjectId()
    {
        // Arrange
        var newProject = BuildProject("proj-abc", "/home/user/abc");
        _sessionService.GetLastSessionForProjectAsync("proj-abc", Arg.Any<CancellationToken>())
            .Returns(BuildSession("sess-1", "proj-abc"));

        // Act
        await PublishProjectChangedAndWaitAsync(newProject);

        // Assert
        await _sessionService.Received(1).GetLastSessionForProjectAsync(
            "proj-abc", Arg.Any<CancellationToken>());
    }
}
