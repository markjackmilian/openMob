using NSubstitute.ExceptionExtensions;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="ProjectsViewModel"/>.
/// </summary>
public sealed class ProjectsViewModelTests
{
    private readonly IProjectService _projectService;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;
    private readonly IActiveProjectService _activeProjectService;
    private readonly ProjectsViewModel _sut;

    public ProjectsViewModelTests()
    {
        _projectService = Substitute.For<IProjectService>();
        _navigationService = Substitute.For<INavigationService>();
        _popupService = Substitute.For<IAppPopupService>();
        _activeProjectService = Substitute.For<IActiveProjectService>();

        _sut = new ProjectsViewModel(_projectService, _navigationService, _popupService, _activeProjectService);
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private static ProjectDto BuildProject(string id = "proj-1", string worktree = "/home/user/myproject")
    {
        var time = new ProjectTimeDto(Created: 1710000000000, Initialized: null);
        return new ProjectDto(Id: id, Worktree: worktree, VcsDir: null, Vcs: "git", Time: time);
    }

    // ─── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesWithEmptyProjectsCollection()
    {
        // Assert
        _sut.Projects.Should().BeEmpty();
        _sut.IsLoading.Should().BeFalse();
        _sut.IsEmpty.Should().BeFalse();
    }

    // ─── LoadProjectsCommand ──────────────────────────────────────────────────

    [Fact]
    public async Task LoadProjectsCommand_WhenServiceReturnsProjects_PopulatesCollection()
    {
        // Arrange
        var projects = new List<ProjectDto>
        {
            BuildProject("p1", "/home/user/alpha"),
            BuildProject("p2", "/home/user/beta"),
        };
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>()).Returns(projects);
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>()).Returns((ProjectDto?)null);

        // Act
        await _sut.LoadProjectsCommand.ExecuteAsync(null);

        // Assert
        _sut.Projects.Should().HaveCount(2);
        _sut.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task LoadProjectsCommand_ExtractsProjectNameFromWorktreePath()
    {
        // Arrange
        var projects = new List<ProjectDto> { BuildProject("p1", "/home/user/myproject") };
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>()).Returns(projects);
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>()).Returns((ProjectDto?)null);

        // Act
        await _sut.LoadProjectsCommand.ExecuteAsync(null);

        // Assert
        _sut.Projects.Should().ContainSingle(p => p.Name == "myproject");
    }

    [Fact]
    public async Task LoadProjectsCommand_WhenEmpty_SetsIsEmptyTrue()
    {
        // Arrange
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProjectDto>());
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);

        // Act
        await _sut.LoadProjectsCommand.ExecuteAsync(null);

        // Assert
        _sut.IsEmpty.Should().BeTrue();
        _sut.Projects.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadProjectsCommand_SetsActiveProjectId()
    {
        // Arrange
        var projects = new List<ProjectDto> { BuildProject("p1") };
        var currentProject = BuildProject("p1");
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>()).Returns(projects);
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>()).Returns(currentProject);

        // Act
        await _sut.LoadProjectsCommand.ExecuteAsync(null);

        // Assert
        _sut.ActiveProjectId.Should().Be("p1");
    }

    [Fact]
    public async Task LoadProjectsCommand_MarksActiveProjectInCollection()
    {
        // Arrange
        var projects = new List<ProjectDto>
        {
            BuildProject("p1", "/path/a"),
            BuildProject("p2", "/path/b"),
        };
        var currentProject = BuildProject("p1");
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>()).Returns(projects);
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>()).Returns(currentProject);

        // Act
        await _sut.LoadProjectsCommand.ExecuteAsync(null);

        // Assert
        _sut.Projects.Should().ContainSingle(p => p.IsActive && p.Id == "p1");
        _sut.Projects.Should().ContainSingle(p => !p.IsActive && p.Id == "p2");
    }

    [Fact]
    public async Task LoadProjectsCommand_SetsIsLoadingFalseAfterCompletion()
    {
        // Arrange
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProjectDto>());
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);

        // Act
        await _sut.LoadProjectsCommand.ExecuteAsync(null);

        // Assert
        _sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadProjectsCommand_WhenServiceThrows_SetsIsEmptyTrue()
    {
        // Arrange
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Error"));

        // Act
        await _sut.LoadProjectsCommand.ExecuteAsync(null);

        // Assert
        _sut.IsEmpty.Should().BeTrue();
        _sut.Projects.Should().BeEmpty();
        _sut.IsLoading.Should().BeFalse();
    }

    // ─── SelectProjectCommand ─────────────────────────────────────────────────

    [Fact]
    public async Task SelectProjectCommand_NavigatesToProjectDetail()
    {
        // Act
        await _sut.SelectProjectCommand.ExecuteAsync("p1");

        // Assert
        await _navigationService.Received(1).GoToAsync(
            "project-detail",
            Arg.Is<IDictionary<string, object>>(d => (string)d["projectId"] == "p1"),
            Arg.Any<CancellationToken>());
    }

    // ─── DeleteProjectCommand ─────────────────────────────────────────────────

    [Fact]
    public async Task DeleteProjectCommand_WhenConfirmed_ShowsToast()
    {
        // Arrange
        _popupService.ShowConfirmDeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProjectDto>());
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);

        // Act
        await _sut.DeleteProjectCommand.ExecuteAsync("p1");

        // Assert
        await _popupService.Received(1).ShowToastAsync(
            Arg.Is<string>(s => s.Contains("managed by the server")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteProjectCommand_WhenNotConfirmed_DoesNotShowToast()
    {
        // Arrange
        _popupService.ShowConfirmDeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.DeleteProjectCommand.ExecuteAsync("p1");

        // Assert
        await _popupService.DidNotReceive().ShowToastAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteProjectCommand_WhenConfirmed_ReloadsProjects()
    {
        // Arrange
        _popupService.ShowConfirmDeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProjectDto>());
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);

        // Act
        await _sut.DeleteProjectCommand.ExecuteAsync("p1");

        // Assert
        await _projectService.Received(1).GetAllProjectsAsync(Arg.Any<CancellationToken>());
    }
}
