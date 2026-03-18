using NSubstitute.ExceptionExtensions;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Models;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="FlyoutViewModel"/>.
/// Covers session loading, deletion with confirmation, new chat creation,
/// session selection navigation, error handling, and constructor guard clauses.
/// </summary>
public sealed class FlyoutViewModelTests
{
    private readonly IProjectService _projectService;
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;
    private readonly FlyoutViewModel _sut;

    public FlyoutViewModelTests()
    {
        _projectService = Substitute.For<IProjectService>();
        _sessionService = Substitute.For<ISessionService>();
        _navigationService = Substitute.For<INavigationService>();
        _popupService = Substitute.For<IAppPopupService>();

        _sut = new FlyoutViewModel(
            _projectService,
            _sessionService,
            _navigationService,
            _popupService);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Builds a <see cref="ProjectDto"/> with sensible defaults for testing.</summary>
    private static ProjectDto BuildProject(
        string id = "proj-1",
        string worktree = "/home/user/myproject",
        string? vcs = "git")
    {
        var time = new ProjectTimeDto(Created: 1710000000000, Initialized: null);
        return new ProjectDto(Id: id, Worktree: worktree, VcsDir: null, Vcs: vcs, Time: time);
    }

    /// <summary>Builds a <see cref="SessionDto"/> with sensible defaults for testing.</summary>
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

    /// <summary>Builds a list of <see cref="SessionDto"/> objects for collection tests.</summary>
    private static IReadOnlyList<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto> BuildSessionList(
        int count = 3, string projectId = "proj-1")
    {
        return Enumerable.Range(1, count)
            .Select(i => BuildSession(
                id: $"sess-{i}",
                projectId: projectId,
                title: $"Session {i}",
                updated: 1710000000000 + i * 1000))
            .ToList();
    }

    /// <summary>
    /// Sets up mocks so that LoadSessionsAsync succeeds with a project and sessions.
    /// Used as a prerequisite for tests that need a populated session list.
    /// </summary>
    private void SetupLoadSessionsSuccess(
        ProjectDto? project = null,
        IReadOnlyList<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>? sessions = null)
    {
        var proj = project ?? BuildProject();
        var sess = sessions ?? BuildSessionList();

        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns(proj);
        _sessionService.GetSessionsByProjectAsync(proj.Id, Arg.Any<CancellationToken>())
            .Returns(sess);
    }

    // ─── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Assert
        _sut.ProjectSectionTitle.Should().BeEmpty();
        _sut.Sessions.Should().BeEmpty();
        _sut.HasProject.Should().BeFalse();
        _sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WhenProjectServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FlyoutViewModel(null!, _sessionService, _navigationService, _popupService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("projectService");
    }

    [Fact]
    public void Constructor_WhenSessionServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FlyoutViewModel(_projectService, null!, _navigationService, _popupService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("sessionService");
    }

    [Fact]
    public void Constructor_WhenNavigationServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FlyoutViewModel(_projectService, _sessionService, null!, _popupService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("navigationService");
    }

    [Fact]
    public void Constructor_WhenPopupServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FlyoutViewModel(_projectService, _sessionService, _navigationService, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("popupService");
    }

    // ─── LoadSessionsCommand — happy path [REQ-033] ───────────────────────────

    [Fact]
    public async Task LoadSessionsCommand_WhenServiceReturnsData_PopulatesSessionsCollection()
    {
        // Arrange
        var sessions = BuildSessionList(2);
        SetupLoadSessionsSuccess(sessions: sessions);

        // Act
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Assert
        _sut.Sessions.Should().Contain(s => s.Id == "sess-1");
        _sut.Sessions.Should().Contain(s => s.Id == "sess-2");
    }

    [Fact]
    public async Task LoadSessionsCommand_WhenServiceReturnsData_SetsHasProjectTrue()
    {
        // Arrange
        SetupLoadSessionsSuccess();

        // Act
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Assert
        _sut.HasProject.Should().BeTrue();
    }

    [Fact]
    public async Task LoadSessionsCommand_WhenServiceReturnsData_SetsProjectSectionTitleToUppercase()
    {
        // Arrange
        var project = BuildProject(worktree: "/home/user/myproject");
        SetupLoadSessionsSuccess(project: project);

        // Act
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Assert
        _sut.ProjectSectionTitle.Should().Be("MYPROJECT");
    }

    [Fact]
    public async Task LoadSessionsCommand_WhenServiceReturnsData_SetsIsLoadingFalse()
    {
        // Arrange
        SetupLoadSessionsSuccess();

        // Act
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Assert
        _sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadSessionsCommand_WhenServiceReturnsData_MapsSessionDtoToSessionItem()
    {
        // Arrange
        var session = BuildSession(id: "sess-42", title: "My Chat", projectId: "proj-1", updated: 1710000005000);
        SetupLoadSessionsSuccess(sessions: new[] { session });

        // Act
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Assert
        _sut.Sessions.Should().ContainSingle();
        var item = _sut.Sessions[0];
        item.Id.Should().Be("sess-42");
        item.Title.Should().Be("My Chat");
        item.ProjectId.Should().Be("proj-1");
        item.UpdatedAt.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1710000005000));
        item.IsSelected.Should().BeFalse();
    }

    // ─── LoadSessionsCommand — error path [REQ-034] ──────────────────────────

    [Fact]
    public async Task LoadSessionsCommand_WhenProjectServiceThrows_ClearsSessions()
    {
        // Arrange
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Assert
        _sut.Sessions.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadSessionsCommand_WhenProjectServiceThrows_SetsIsLoadingFalse()
    {
        // Arrange
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Assert
        _sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadSessionsCommand_WhenSessionServiceThrows_ClearsSessions()
    {
        // Arrange
        var project = BuildProject();
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns(project);
        _sessionService.GetSessionsByProjectAsync(project.Id, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Service failure"));

        // Act
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Assert
        _sut.Sessions.Should().BeEmpty();
    }

    // ─── LoadSessionsCommand — no project ─────────────────────────────────────

    [Fact]
    public async Task LoadSessionsCommand_WhenNoCurrentProject_SetsHasProjectFalse()
    {
        // Arrange
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);

        // Act
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Assert
        _sut.HasProject.Should().BeFalse();
    }

    [Fact]
    public async Task LoadSessionsCommand_WhenNoCurrentProject_ClearsProjectSectionTitle()
    {
        // Arrange
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);

        // Act
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Assert
        _sut.ProjectSectionTitle.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadSessionsCommand_WhenNoCurrentProject_ClearsSessions()
    {
        // Arrange
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);

        // Act
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Assert
        _sut.Sessions.Should().BeEmpty();
    }

    // ─── LoadSessionsCommand — loading state ──────────────────────────────────

    [Fact]
    public async Task LoadSessionsCommand_SetsIsLoadingDuringExecution()
    {
        // Arrange
        var isLoadingDuringCall = false;
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                isLoadingDuringCall = _sut.IsLoading;
                return BuildProject();
            });
        _sessionService.GetSessionsByProjectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(BuildSessionList(1));

        // Act
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Assert
        isLoadingDuringCall.Should().BeTrue();
        _sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadSessionsCommand_WhenAlreadyLoading_DoesNotCallServiceAgain()
    {
        // Arrange
        _sut.IsLoading = true;

        // Act
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Assert
        await _projectService.DidNotReceive().GetCurrentProjectAsync(Arg.Any<CancellationToken>());
    }

    // ─── DeleteSessionCommand — confirmed and succeeds [REQ-035] ──────────────

    [Fact]
    public async Task DeleteSessionCommand_WhenConfirmedAndSucceeds_CallsDeleteOnService()
    {
        // Arrange
        _popupService.ShowConfirmDeleteAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _sessionService.DeleteSessionAsync("sess-1", Arg.Any<CancellationToken>())
            .Returns(true);
        SetupLoadSessionsSuccess();

        // Act
        await _sut.DeleteSessionCommand.ExecuteAsync("sess-1");

        // Assert
        await _sessionService.Received(1).DeleteSessionAsync("sess-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSessionCommand_WhenConfirmedAndSucceeds_ShowsToast()
    {
        // Arrange
        _popupService.ShowConfirmDeleteAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _sessionService.DeleteSessionAsync("sess-1", Arg.Any<CancellationToken>())
            .Returns(true);
        SetupLoadSessionsSuccess();

        // Act
        await _sut.DeleteSessionCommand.ExecuteAsync("sess-1");

        // Assert
        await _popupService.Received(1).ShowToastAsync("Session deleted.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSessionCommand_WhenConfirmedAndSucceeds_ReloadsSessionList()
    {
        // Arrange
        _popupService.ShowConfirmDeleteAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _sessionService.DeleteSessionAsync("sess-1", Arg.Any<CancellationToken>())
            .Returns(true);
        SetupLoadSessionsSuccess();

        // Act
        await _sut.DeleteSessionCommand.ExecuteAsync("sess-1");

        // Assert — LoadSessionsAsync calls GetCurrentProjectAsync internally
        await _projectService.Received().GetCurrentProjectAsync(Arg.Any<CancellationToken>());
    }

    // ─── DeleteSessionCommand — user cancels ──────────────────────────────────

    [Fact]
    public async Task DeleteSessionCommand_WhenUserCancelsConfirmation_DoesNotDelete()
    {
        // Arrange
        _popupService.ShowConfirmDeleteAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.DeleteSessionCommand.ExecuteAsync("sess-1");

        // Assert
        await _sessionService.DidNotReceive().DeleteSessionAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ─── DeleteSessionCommand — delete fails ──────────────────────────────────

    [Fact]
    public async Task DeleteSessionCommand_WhenDeleteReturnsFalse_ShowsError()
    {
        // Arrange
        _popupService.ShowConfirmDeleteAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _sessionService.DeleteSessionAsync("sess-1", Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.DeleteSessionCommand.ExecuteAsync("sess-1");

        // Assert
        await _popupService.Received(1).ShowErrorAsync(
            "Error",
            "Failed to delete the session.",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteSessionCommand_WhenDeleteReturnsFalse_DoesNotShowToast()
    {
        // Arrange
        _popupService.ShowConfirmDeleteAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _sessionService.DeleteSessionAsync("sess-1", Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.DeleteSessionCommand.ExecuteAsync("sess-1");

        // Assert
        await _popupService.DidNotReceive().ShowToastAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ─── DeleteSessionCommand — service throws ────────────────────────────────

    [Fact]
    public async Task DeleteSessionCommand_WhenServiceThrows_ShowsError()
    {
        // Arrange
        _popupService.ShowConfirmDeleteAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _sessionService.DeleteSessionAsync("sess-1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        await _sut.DeleteSessionCommand.ExecuteAsync("sess-1");

        // Assert
        await _popupService.Received(1).ShowErrorAsync(
            "Error",
            "Failed to delete the session.",
            Arg.Any<CancellationToken>());
    }

    // ─── DeleteSessionCommand — argument validation ───────────────────────────

    [Fact]
    public async Task DeleteSessionCommand_WhenSessionIdIsNull_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _sut.DeleteSessionCommand.ExecuteAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteSessionCommand_WhenSessionIdIsEmpty_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _sut.DeleteSessionCommand.ExecuteAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteSessionCommand_WhenSessionIdIsWhitespace_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _sut.DeleteSessionCommand.ExecuteAsync("   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── NewChatCommand — happy path [REQ-036] ────────────────────────────────

    [Fact]
    public async Task NewChatCommand_WhenSessionCreated_NavigatesToChatWithSessionId()
    {
        // Arrange
        var session = BuildSession(id: "new-sess-1", title: "New Chat");
        _sessionService.CreateSessionAsync(null, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        await _sut.NewChatCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.Received(1).GoToAsync(
            "//chat",
            Arg.Is<IDictionary<string, object>>(d => d["sessionId"].Equals("new-sess-1")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewChatCommand_WhenSessionCreated_CallsCreateWithNullTitle()
    {
        // Arrange
        var session = BuildSession(id: "new-sess-1");
        _sessionService.CreateSessionAsync(null, Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        await _sut.NewChatCommand.ExecuteAsync(null);

        // Assert
        await _sessionService.Received(1).CreateSessionAsync(null, Arg.Any<CancellationToken>());
    }

    // ─── NewChatCommand — create fails [REQ-036 error path] ───────────────────

    [Fact]
    public async Task NewChatCommand_WhenCreateReturnsNull_ShowsError()
    {
        // Arrange
        _sessionService.CreateSessionAsync(null, Arg.Any<CancellationToken>())
            .Returns((openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto?)null);

        // Act
        await _sut.NewChatCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowErrorAsync(
            "Error",
            "Failed to create a new session.",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewChatCommand_WhenCreateReturnsNull_DoesNotNavigate()
    {
        // Arrange
        _sessionService.CreateSessionAsync(null, Arg.Any<CancellationToken>())
            .Returns((openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto?)null);

        // Act
        await _sut.NewChatCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.DidNotReceive().GoToAsync(
            Arg.Any<string>(),
            Arg.Any<IDictionary<string, object>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewChatCommand_WhenServiceThrows_ShowsError()
    {
        // Arrange
        _sessionService.CreateSessionAsync(null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        await _sut.NewChatCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowErrorAsync(
            "Error",
            "Failed to create a new session.",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewChatCommand_WhenServiceThrows_DoesNotNavigate()
    {
        // Arrange
        _sessionService.CreateSessionAsync(null, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        await _sut.NewChatCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.DidNotReceive().GoToAsync(
            Arg.Any<string>(),
            Arg.Any<IDictionary<string, object>>(),
            Arg.Any<CancellationToken>());
    }

    // ─── SelectSessionCommand [REQ-037] ───────────────────────────────────────

    [Fact]
    public async Task SelectSessionCommand_WhenExecuted_NavigatesToChatWithSessionId()
    {
        // Act
        await _sut.SelectSessionCommand.ExecuteAsync("sess-42");

        // Assert
        await _navigationService.Received(1).GoToAsync(
            "//chat",
            Arg.Is<IDictionary<string, object>>(d => d["sessionId"].Equals("sess-42")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectSessionCommand_WhenExecutedWithDifferentId_PassesCorrectSessionId()
    {
        // Act
        await _sut.SelectSessionCommand.ExecuteAsync("sess-99");

        // Assert
        await _navigationService.Received(1).GoToAsync(
            "//chat",
            Arg.Is<IDictionary<string, object>>(d => d["sessionId"].Equals("sess-99")),
            Arg.Any<CancellationToken>());
    }

    // ─── SelectSessionCommand — argument validation ───────────────────────────

    [Fact]
    public async Task SelectSessionCommand_WhenSessionIdIsNull_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _sut.SelectSessionCommand.ExecuteAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SelectSessionCommand_WhenSessionIdIsEmpty_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _sut.SelectSessionCommand.ExecuteAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SelectSessionCommand_WhenSessionIdIsWhitespace_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _sut.SelectSessionCommand.ExecuteAsync("   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
