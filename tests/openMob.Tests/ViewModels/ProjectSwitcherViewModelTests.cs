using NSubstitute.ExceptionExtensions;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="ProjectSwitcherViewModel"/>.
/// </summary>
public sealed class ProjectSwitcherViewModelTests
{
    private readonly IProjectService _projectService;
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;
    private readonly ProjectSwitcherViewModel _sut;

    public ProjectSwitcherViewModelTests()
    {
        _projectService = Substitute.For<IProjectService>();
        _sessionService = Substitute.For<ISessionService>();
        _navigationService = Substitute.For<INavigationService>();
        _popupService = Substitute.For<IAppPopupService>();

        _sut = new ProjectSwitcherViewModel(
            _projectService, _sessionService, _navigationService, _popupService);
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
        long updated = 1710000001000)
    {
        var time = new SessionTimeDto(Created: 1710000000000, Updated: updated, Compacting: null);
        return new openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto(
            Id: id, ProjectId: projectId, Directory: "/path", ParentId: null,
            Summary: null, Share: null, Title: "Test", Version: "1",
            Time: time, Revert: null);
    }

    // ─── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesWithEmptyProjectsCollection()
    {
        // Assert
        _sut.Projects.Should().BeEmpty();
        _sut.IsLoading.Should().BeFalse();
    }

    // ─── LoadProjectsCommand ──────────────────────────────────────────────────

    [Fact]
    public async Task LoadProjectsCommand_PopulatesProjectsCollection()
    {
        // Arrange
        var projects = new List<ProjectDto>
        {
            BuildProject("p1", "/path/alpha"),
            BuildProject("p2", "/path/beta"),
        };
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>()).Returns(projects);
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>()).Returns(projects[0]);

        // Act
        await _sut.LoadProjectsCommand.ExecuteAsync(null);

        // Assert
        _sut.Projects.Should().HaveCount(2);
        _sut.ActiveProjectId.Should().Be("p1");
    }

    [Fact]
    public async Task LoadProjectsCommand_SetsIsLoadingFalseAfterCompletion()
    {
        // Arrange
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProjectDto>());
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);

        // Act
        await _sut.LoadProjectsCommand.ExecuteAsync(null);

        // Assert
        _sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadProjectsCommand_WhenServiceThrows_SetsEmptyCollection()
    {
        // Arrange
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Error"));

        // Act
        await _sut.LoadProjectsCommand.ExecuteAsync(null);

        // Assert
        _sut.Projects.Should().BeEmpty();
        _sut.IsLoading.Should().BeFalse();
    }

    // ─── SelectProjectCommand ─────────────────────────────────────────────────

    [Fact]
    public async Task SelectProjectCommand_WhenLastSessionExists_NavigatesWithSessionId()
    {
        // Arrange
        var lastSession = BuildSession("sess-last", "proj-1");
        _sessionService.GetLastSessionForProjectAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(lastSession);

        // Act
        await _sut.SelectProjectCommand.ExecuteAsync("proj-1");

        // Assert
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
        await _navigationService.Received(1).GoToAsync(
            "//chat",
            Arg.Is<IDictionary<string, object>>(d => (string)d["sessionId"] == "sess-last"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectProjectCommand_WhenNoSession_CreatesNewAndNavigates()
    {
        // Arrange
        _sessionService.GetLastSessionForProjectAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns((openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto?)null);

        var newSession = BuildSession("new-sess", "proj-1");
        _sessionService.CreateSessionAsync(null, Arg.Any<CancellationToken>())
            .Returns(newSession);

        // Act
        await _sut.SelectProjectCommand.ExecuteAsync("proj-1");

        // Assert
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
        await _navigationService.Received(1).GoToAsync(
            "//chat",
            Arg.Is<IDictionary<string, object>>(d => (string)d["sessionId"] == "new-sess"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectProjectCommand_WhenNoSessionAndCreateFails_NavigatesToChatWithoutSession()
    {
        // Arrange
        _sessionService.GetLastSessionForProjectAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns((openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto?)null);
        _sessionService.CreateSessionAsync(null, Arg.Any<CancellationToken>())
            .Returns((openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto?)null);

        // Act
        await _sut.SelectProjectCommand.ExecuteAsync("proj-1");

        // Assert
        await _navigationService.Received(1).GoToAsync("//chat", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectProjectCommand_ClosesPopupFirst()
    {
        // Arrange
        _sessionService.GetLastSessionForProjectAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildSession());

        // Act
        await _sut.SelectProjectCommand.ExecuteAsync("proj-1");

        // Assert
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
    }

    // ─── ManageProjectsCommand ────────────────────────────────────────────────

    [Fact]
    public async Task ManageProjectsCommand_ClosesPopupAndNavigatesToProjects()
    {
        // Act
        await _sut.ManageProjectsCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
        await _navigationService.Received(1).GoToAsync("projects", Arg.Any<CancellationToken>());
    }
}
