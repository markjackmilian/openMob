using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Services;
using OpencodeSessionDto = openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto;

namespace openMob.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ProjectService"/>.
/// </summary>
public sealed class ProjectServiceTests
{
    private readonly IOpencodeApiClient _apiClient;
    private readonly ProjectService _sut;

    public ProjectServiceTests()
    {
        _apiClient = Substitute.For<IOpencodeApiClient>();
        _sut = new ProjectService(_apiClient);
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private static ProjectDto BuildProject(
        string id = "proj-1",
        string worktree = "/home/user/myproject",
        string? vcs = "git")
    {
        var time = new ProjectTimeDto(Created: 1710000000000, Initialized: null);
        return new ProjectDto(Id: id, Worktree: worktree, VcsDir: null, Vcs: vcs, Time: time);
    }

    private static OpencodeSessionDto BuildSession(
        string id = "session-1",
        string projectId = "proj-1",
        string directory = "/home/user/myproject")
    {
        var time = new SessionTimeDto(Created: 1710000000000, Updated: 1710000000000, Compacting: null);
        return new OpencodeSessionDto(
            Id: id,
            ProjectId: projectId,
            Directory: directory,
            ParentId: null,
            Summary: null,
            Share: null,
            Title: string.Empty,
            Version: "1.0",
            Time: time,
            Revert: null);
    }

    // ─── GetAllProjectsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAllProjectsAsync_WhenApiReturnsProjects_ReturnsProjectList()
    {
        // Arrange
        var projects = new List<ProjectDto> { BuildProject("p1"), BuildProject("p2") };
        _apiClient.GetProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<ProjectDto>>.Success(projects));

        // Act
        var result = await _sut.GetAllProjectsAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllProjectsAsync_WhenApiReturnsFailure_ReturnsEmptyList()
    {
        // Arrange
        _apiClient.GetProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<ProjectDto>>.Failure(
                new OpencodeApiError(ErrorKind.ServerError, "Server error", 500, null)));

        // Act
        var result = await _sut.GetAllProjectsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllProjectsAsync_CallsApiClientExactlyOnce()
    {
        // Arrange
        _apiClient.GetProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<ProjectDto>>.Success(new List<ProjectDto>()));

        // Act
        await _sut.GetAllProjectsAsync();

        // Assert
        await _apiClient.Received(1).GetProjectsAsync(Arg.Any<CancellationToken>());
    }

    // ─── GetCurrentProjectAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentProjectAsync_WhenApiReturnsProject_ReturnsProject()
    {
        // Arrange
        var project = BuildProject();
        _apiClient.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ProjectDto>.Success(project));

        // Act
        var result = await _sut.GetCurrentProjectAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("proj-1");
    }

    [Fact]
    public async Task GetCurrentProjectAsync_WhenApiReturnsNotFoundError_ReturnsNull()
    {
        // Arrange
        _apiClient.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ProjectDto>.Failure(
                new OpencodeApiError(ErrorKind.NotFound, "Not found", 404, null)));

        // Act
        var result = await _sut.GetCurrentProjectAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentProjectAsync_WhenApiReturnsServerError_ReturnsNull()
    {
        // Arrange
        _apiClient.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ProjectDto>.Failure(
                new OpencodeApiError(ErrorKind.ServerError, "Server error", 500, null)));

        // Act
        var result = await _sut.GetCurrentProjectAsync();

        // Assert
        result.Should().BeNull();
    }

    // ─── GetProjectByIdAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetProjectByIdAsync_WhenProjectExists_ReturnsMatchingProject()
    {
        // Arrange
        var projects = new List<ProjectDto>
        {
            BuildProject("p1", "/path/a"),
            BuildProject("p2", "/path/b"),
        };
        _apiClient.GetProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<ProjectDto>>.Success(projects));

        // Act
        var result = await _sut.GetProjectByIdAsync("p2");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("p2");
    }

    [Fact]
    public async Task GetProjectByIdAsync_WhenProjectDoesNotExist_ReturnsNull()
    {
        // Arrange
        var projects = new List<ProjectDto> { BuildProject("p1") };
        _apiClient.GetProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<ProjectDto>>.Success(projects));

        // Act
        var result = await _sut.GetProjectByIdAsync("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetProjectByIdAsync_WhenIdIsNullOrWhitespace_ThrowsArgumentException(string? id)
    {
        // Act
        var act = async () => await _sut.GetProjectByIdAsync(id!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── GetProjectByWorktreeAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetProjectByWorktreeAsync_WhenProjectExists_ReturnsMatchingProject()
    {
        // Arrange
        var projects = new List<ProjectDto>
        {
            BuildProject("p1", "/path/a"),
            BuildProject("p2", "/path/b"),
        };
        _apiClient.GetProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<ProjectDto>>.Success(projects));

        // Act
        var result = await _sut.GetProjectByWorktreeAsync("/path/b");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("p2");
    }

    [Fact]
    public async Task GetProjectByWorktreeAsync_WhenProjectDoesNotExist_ReturnsNull()
    {
        // Arrange
        var projects = new List<ProjectDto> { BuildProject("p1", "/path/a") };
        _apiClient.GetProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<ProjectDto>>.Success(projects));

        // Act
        var result = await _sut.GetProjectByWorktreeAsync("/path/missing");

        // Assert
        result.Should().BeNull();
    }

    // ─── EnsureProjectForWorktreeAsync ────────────────────────────────────────

    [Fact]
    public async Task EnsureProjectForWorktreeAsync_WhenProjectExists_DoesNotCreateSession()
    {
        // Arrange
        var projects = new List<ProjectDto> { BuildProject("p1", "/path/a") };
        _apiClient.GetProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<ProjectDto>>.Success(projects));

        // Act
        var result = await _sut.EnsureProjectForWorktreeAsync("/path/a");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("p1");
        await _apiClient.DidNotReceive().CreateSessionForDirectoryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureProjectForWorktreeAsync_WhenProjectMissing_CreatesSessionAndReturnsProject()
    {
        // Arrange
        var project = BuildProject("p2", "/path/new");
        var projects = new List<ProjectDto> { BuildProject("p1", "/path/a") };
        _apiClient.GetProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(
                OpencodeResult<IReadOnlyList<ProjectDto>>.Success(projects),
                OpencodeResult<IReadOnlyList<ProjectDto>>.Success(new List<ProjectDto> { BuildProject("p1", "/path/a"), project }));
        _apiClient.CreateSessionForDirectoryAsync("/path/new", Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<OpencodeSessionDto>.Success(BuildSession(projectId: "p2", directory: "/path/new")));

        // Act
        var result = await _sut.EnsureProjectForWorktreeAsync("/path/new");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("p2");
        await _apiClient.Received(1).CreateSessionForDirectoryAsync("/path/new", Arg.Any<CancellationToken>());
    }
}
