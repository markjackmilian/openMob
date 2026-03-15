using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="ProjectDetailViewModel"/>.
/// </summary>
public sealed class ProjectDetailViewModelTests
{
    private readonly IProjectService _projectService;
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;
    private readonly ProjectDetailViewModel _sut;

    public ProjectDetailViewModelTests()
    {
        _projectService = Substitute.For<IProjectService>();
        _sessionService = Substitute.For<ISessionService>();
        _navigationService = Substitute.For<INavigationService>();
        _popupService = Substitute.For<IAppPopupService>();

        _sut = new ProjectDetailViewModel(
            _projectService, _sessionService, _navigationService, _popupService);
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
        _sut.ProjectId.Should().BeEmpty();
        _sut.ProjectName.Should().BeEmpty();
        _sut.IsLoading.Should().BeFalse();
        _sut.RecentSessions.Should().BeEmpty();
    }

    // ─── LoadProjectCommand ───────────────────────────────────────────────────

    [Fact]
    public async Task LoadProjectCommand_WhenProjectExists_SetsProjectProperties()
    {
        // Arrange
        var project = BuildProject("p1", "/home/user/myproject", "git");
        _projectService.GetProjectByIdAsync("p1", Arg.Any<CancellationToken>()).Returns(project);
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>()).Returns(project);
        _sessionService.GetSessionsByProjectAsync("p1", Arg.Any<CancellationToken>())
            .Returns(new List<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>());

        // Act
        await _sut.LoadProjectCommand.ExecuteAsync("p1");

        // Assert
        _sut.ProjectId.Should().Be("p1");
        _sut.ProjectName.Should().Be("myproject");
        _sut.ProjectPath.Should().Be("/home/user/myproject");
        _sut.ProjectDescription.Should().Contain("git");
    }

    [Fact]
    public async Task LoadProjectCommand_WhenProjectIsActive_SetsIsActiveProjectTrue()
    {
        // Arrange
        var project = BuildProject("p1");
        _projectService.GetProjectByIdAsync("p1", Arg.Any<CancellationToken>()).Returns(project);
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>()).Returns(project);
        _sessionService.GetSessionsByProjectAsync("p1", Arg.Any<CancellationToken>())
            .Returns(new List<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>());

        // Act
        await _sut.LoadProjectCommand.ExecuteAsync("p1");

        // Assert
        _sut.IsActiveProject.Should().BeTrue();
    }

    [Fact]
    public async Task LoadProjectCommand_WhenProjectIsNotActive_SetsIsActiveProjectFalse()
    {
        // Arrange
        var project = BuildProject("p1");
        var otherProject = BuildProject("p2");
        _projectService.GetProjectByIdAsync("p1", Arg.Any<CancellationToken>()).Returns(project);
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>()).Returns(otherProject);
        _sessionService.GetSessionsByProjectAsync("p1", Arg.Any<CancellationToken>())
            .Returns(new List<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>());

        // Act
        await _sut.LoadProjectCommand.ExecuteAsync("p1");

        // Assert
        _sut.IsActiveProject.Should().BeFalse();
    }

    [Fact]
    public async Task LoadProjectCommand_LoadsRecentSessionsMax5()
    {
        // Arrange
        var project = BuildProject("p1");
        _projectService.GetProjectByIdAsync("p1", Arg.Any<CancellationToken>()).Returns(project);
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>()).Returns((ProjectDto?)null);

        var sessions = Enumerable.Range(1, 7)
            .Select(i => BuildSession($"s{i}", "p1", $"Session {i}", updated: 1710000000000 + i * 1000))
            .ToList();
        _sessionService.GetSessionsByProjectAsync("p1", Arg.Any<CancellationToken>())
            .Returns(sessions);

        // Act
        await _sut.LoadProjectCommand.ExecuteAsync("p1");

        // Assert
        _sut.RecentSessions.Should().HaveCount(5);
    }

    [Fact]
    public async Task LoadProjectCommand_WhenProjectNotFound_ShowsErrorAndPops()
    {
        // Arrange
        _projectService.GetProjectByIdAsync("missing", Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);

        // Act
        await _sut.LoadProjectCommand.ExecuteAsync("missing");

        // Assert
        await _popupService.Received(1).ShowErrorAsync(
            "Project Not Found",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await _navigationService.Received(1).PopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadProjectCommand_SetsIsLoadingFalseAfterCompletion()
    {
        // Arrange
        var project = BuildProject("p1");
        _projectService.GetProjectByIdAsync("p1", Arg.Any<CancellationToken>()).Returns(project);
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>()).Returns((ProjectDto?)null);
        _sessionService.GetSessionsByProjectAsync("p1", Arg.Any<CancellationToken>())
            .Returns(new List<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>());

        // Act
        await _sut.LoadProjectCommand.ExecuteAsync("p1");

        // Assert
        _sut.IsLoading.Should().BeFalse();
    }

    // ─── SetActiveCommand ─────────────────────────────────────────────────────

    [Fact]
    public async Task SetActiveCommand_SetsIsActiveProjectTrue()
    {
        // Act
        await _sut.SetActiveCommand.ExecuteAsync(null);

        // Assert
        _sut.IsActiveProject.Should().BeTrue();
    }

    [Fact]
    public async Task SetActiveCommand_ShowsToast()
    {
        // Arrange
        _sut.ProjectName = "MyProject";

        // Act
        await _sut.SetActiveCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowToastAsync(
            Arg.Is<string>(s => s.Contains("MyProject")),
            Arg.Any<CancellationToken>());
    }

    // ─── NewSessionCommand ────────────────────────────────────────────────────

    [Fact]
    public async Task NewSessionCommand_WhenSessionCreated_NavigatesToChat()
    {
        // Arrange
        var session = BuildSession("new-sess");
        _sessionService.CreateSessionAsync(null, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        await _sut.NewSessionCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.Received(1).GoToAsync(
            "//chat",
            Arg.Is<IDictionary<string, object>>(d => (string)d["sessionId"] == "new-sess"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewSessionCommand_WhenSessionCreationFails_ShowsError()
    {
        // Arrange
        _sessionService.CreateSessionAsync(null, Arg.Any<CancellationToken>())
            .Returns((openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto?)null);

        // Act
        await _sut.NewSessionCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowErrorAsync(
            "Error",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    // ─── DeleteProjectCommand ─────────────────────────────────────────────────

    [Fact]
    public async Task DeleteProjectCommand_WhenConfirmed_ShowsToastAndPops()
    {
        // Arrange
        _popupService.ShowConfirmDeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _sut.DeleteProjectCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowToastAsync(
            Arg.Is<string>(s => s.Contains("managed by the server")),
            Arg.Any<CancellationToken>());
        await _navigationService.Received(1).PopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteProjectCommand_WhenNotConfirmed_DoesNotPop()
    {
        // Arrange
        _popupService.ShowConfirmDeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.DeleteProjectCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.DidNotReceive().PopAsync(Arg.Any<CancellationToken>());
    }

    // ─── LoadProjectCommand — VCS description ─────────────────────────────────

    [Fact]
    public async Task LoadProjectCommand_WhenVcsIsNull_SetsEmptyDescription()
    {
        // Arrange
        var project = BuildProject("p1", "/path", vcs: null);
        _projectService.GetProjectByIdAsync("p1", Arg.Any<CancellationToken>()).Returns(project);
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>()).Returns((ProjectDto?)null);
        _sessionService.GetSessionsByProjectAsync("p1", Arg.Any<CancellationToken>())
            .Returns(new List<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>());

        // Act
        await _sut.LoadProjectCommand.ExecuteAsync("p1");

        // Assert
        _sut.ProjectDescription.Should().BeEmpty();
    }
}
