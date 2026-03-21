using CommunityToolkit.Mvvm.Messaging;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Messages;
using openMob.Core.Services;

namespace openMob.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ActiveProjectService"/>.
/// Verifies lazy initialisation, caching, project switching, and message publishing.
/// </summary>
public sealed class ActiveProjectServiceTests : IDisposable
{
    private readonly IProjectService _projectService;
    private readonly IAppStateService _appStateService;
    private readonly ActiveProjectService _sut;

    public ActiveProjectServiceTests()
    {
        // Ensure a clean messenger state for each test
        WeakReferenceMessenger.Default.Reset();

        _projectService = Substitute.For<IProjectService>();
        _appStateService = Substitute.For<IAppStateService>();
        _sut = new ActiveProjectService(_projectService, _appStateService);
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        WeakReferenceMessenger.Default.Reset();
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

    // ─── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WhenProjectServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ActiveProjectService(null!, _appStateService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("projectService");
    }

    [Fact]
    public void Constructor_WhenAppStateServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ActiveProjectService(_projectService, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("appStateService");
    }

    // ─── GetActiveProjectAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetActiveProjectAsync_WhenCalledFirstTime_FetchesFromProjectService()
    {
        // Arrange
        var project = BuildProject();
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns(project);

        // Act
        await _sut.GetActiveProjectAsync();

        // Assert
        await _projectService.Received(1).GetCurrentProjectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetActiveProjectAsync_WhenCalledFirstTime_ReturnsProjectFromService()
    {
        // Arrange
        var project = BuildProject("proj-42");
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns(project);

        // Act
        var result = await _sut.GetActiveProjectAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("proj-42");
    }

    [Fact]
    public async Task GetActiveProjectAsync_WhenCalledTwice_ReturnsCachedValueWithoutRefetching()
    {
        // Arrange
        var project = BuildProject();
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns(project);

        // Act
        var first = await _sut.GetActiveProjectAsync();
        var second = await _sut.GetActiveProjectAsync();

        // Assert
        await _projectService.Received(1).GetCurrentProjectAsync(Arg.Any<CancellationToken>());
        second.Should().BeSameAs(first);
    }

    [Fact]
    public async Task GetActiveProjectAsync_WhenServerReturnsNull_CachesNullAndDoesNotRefetch()
    {
        // Arrange
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);

        // Act
        var first = await _sut.GetActiveProjectAsync();
        var second = await _sut.GetActiveProjectAsync();

        // Assert
        first.Should().BeNull();
        second.Should().BeNull();
        await _projectService.Received(1).GetCurrentProjectAsync(Arg.Any<CancellationToken>());
    }

    // ─── SetActiveProjectAsync — happy path ───────────────────────────────────

    [Fact]
    public async Task SetActiveProjectAsync_WhenProjectExists_ReturnsTrue()
    {
        // Arrange
        var project = BuildProject("proj-new");
        _projectService.GetProjectByIdAsync("proj-new", Arg.Any<CancellationToken>())
            .Returns(project);

        // Act
        var result = await _sut.SetActiveProjectAsync("proj-new");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SetActiveProjectAsync_WhenProjectExists_PublishesActiveProjectChangedMessage()
    {
        // Arrange
        var project = BuildProject("proj-new");
        _projectService.GetProjectByIdAsync("proj-new", Arg.Any<CancellationToken>())
            .Returns(project);

        ActiveProjectChangedMessage? receivedMessage = null;
        WeakReferenceMessenger.Default.Register<ActiveProjectChangedMessage>(this, (_, m) => receivedMessage = m);

        // Act
        await _sut.SetActiveProjectAsync("proj-new");

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.Project.Id.Should().Be("proj-new");
    }

    [Fact]
    public async Task SetActiveProjectAsync_WhenCalled_SubsequentGetReturnsNewProject()
    {
        // Arrange — initialise with original project
        var original = BuildProject("proj-original");
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns(original);

        var replacement = BuildProject("proj-replacement");
        _projectService.GetProjectByIdAsync("proj-replacement", Arg.Any<CancellationToken>())
            .Returns(replacement);

        // Pre-condition: first Get returns original
        var before = await _sut.GetActiveProjectAsync();
        before!.Id.Should().Be("proj-original");

        // Act
        await _sut.SetActiveProjectAsync("proj-replacement");
        var after = await _sut.GetActiveProjectAsync();

        // Assert
        after.Should().NotBeNull();
        after!.Id.Should().Be("proj-replacement");
    }

    [Fact]
    public async Task SetActiveProjectAsync_WhenProjectExists_CallsGetProjectByIdAsyncOnce()
    {
        // Arrange
        var project = BuildProject("proj-1");
        _projectService.GetProjectByIdAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(project);

        // Act
        await _sut.SetActiveProjectAsync("proj-1");

        // Assert
        await _projectService.Received(1).GetProjectByIdAsync("proj-1", Arg.Any<CancellationToken>());
    }

    // ─── SetActiveProjectAsync — project not found ────────────────────────────

    [Fact]
    public async Task SetActiveProjectAsync_WhenProjectNotFound_ReturnsFalse()
    {
        // Arrange
        _projectService.GetProjectByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);

        // Act
        var result = await _sut.SetActiveProjectAsync("nonexistent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetActiveProjectAsync_WhenProjectNotFound_DoesNotPublishMessage()
    {
        // Arrange
        _projectService.GetProjectByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);

        ActiveProjectChangedMessage? receivedMessage = null;
        WeakReferenceMessenger.Default.Register<ActiveProjectChangedMessage>(this, (_, m) => receivedMessage = m);

        // Act
        await _sut.SetActiveProjectAsync("nonexistent");

        // Assert
        receivedMessage.Should().BeNull();
    }

    [Fact]
    public async Task SetActiveProjectAsync_WhenProjectNotFound_DoesNotOverrideCachedProject()
    {
        // Arrange — initialise with original project
        var original = BuildProject("proj-original");
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns(original);

        _projectService.GetProjectByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);

        // Pre-condition: initialise cache
        await _sut.GetActiveProjectAsync();

        // Act
        await _sut.SetActiveProjectAsync("nonexistent");
        var result = await _sut.GetActiveProjectAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("proj-original");
    }

    // ─── SetActiveProjectAsync — input validation ─────────────────────────────

    [Fact]
    public async Task SetActiveProjectAsync_WhenProjectIdIsNull_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _sut.SetActiveProjectAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetActiveProjectAsync_WhenProjectIdIsEmpty_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _sut.SetActiveProjectAsync(string.Empty);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetActiveProjectAsync_WhenProjectIdIsWhitespace_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _sut.SetActiveProjectAsync("   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task SetActiveProjectAsync_WhenProjectIdIsNullOrWhitespace_ThrowsArgumentException(string? projectId)
    {
        // Act
        var act = async () => await _sut.SetActiveProjectAsync(projectId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetActiveProjectAsync_WhenProjectIdIsInvalid_DoesNotCallProjectService(string? projectId)
    {
        // Act
        try { await _sut.SetActiveProjectAsync(projectId!); } catch { /* expected */ }

        // Assert
        await _projectService.DidNotReceive().GetProjectByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ─── SetActiveProjectAsync — persistence via IAppStateService ─────────────

    [Fact]
    public async Task SetActiveProjectAsync_WhenSuccessful_PersistsProjectId()
    {
        // Arrange
        var project = BuildProject("proj-persist");
        _projectService.GetProjectByIdAsync("proj-persist", Arg.Any<CancellationToken>())
            .Returns(project);

        // Act
        await _sut.SetActiveProjectAsync("proj-persist");

        // Assert
        await _appStateService.Received(1).SetLastActiveProjectIdAsync("proj-persist", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetActiveProjectAsync_WhenProjectNotFound_DoesNotPersist()
    {
        // Arrange
        _projectService.GetProjectByIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);

        // Act
        await _sut.SetActiveProjectAsync("nonexistent");

        // Assert
        await _appStateService.DidNotReceive().SetLastActiveProjectIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
