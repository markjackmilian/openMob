using CommunityToolkit.Mvvm.Messaging;
using NSubstitute.ExceptionExtensions;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Messages;
using openMob.Core.Models;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="FlyoutViewModel"/>.
/// Covers session loading, new chat creation, session selection navigation,
/// messenger-driven state updates, IDisposable cleanup, and constructor guard clauses.
/// </summary>
/// <remarks>
/// Placed in <see cref="MessengerTestCollection"/> to prevent parallel execution with
/// <see cref="ContextSheetViewModelTests"/>, which publishes <see cref="SessionDeletedMessage"/>
/// that <see cref="FlyoutViewModel"/> subscribes to via <see cref="WeakReferenceMessenger.Default"/>.
/// </remarks>
[Collection(MessengerTestCollection.Name)]
public sealed class FlyoutViewModelTests : IDisposable
{
    private readonly IProjectService _projectService;
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;
    private readonly IDispatcherService _dispatcher;
    private readonly IActiveProjectService _activeProjectService;
    private readonly FlyoutViewModel _sut;
    private bool _sutDisposed;

    public FlyoutViewModelTests()
    {
        _projectService = Substitute.For<IProjectService>();
        _sessionService = Substitute.For<ISessionService>();
        _navigationService = Substitute.For<INavigationService>();
        _popupService = Substitute.For<IAppPopupService>();
        _dispatcher = Substitute.For<IDispatcherService>();
        _activeProjectService = Substitute.For<IActiveProjectService>();

        // CRITICAL: dispatcher must execute the action synchronously so that
        // Sessions assignments and CurrentSessionChangedMessage loop updates
        // are visible immediately after the awaited command or Send() call.
        _dispatcher.When(d => d.Dispatch(Arg.Any<Action>())).Do(ci => ci.Arg<Action>()());

        _sut = new FlyoutViewModel(
            _projectService,
            _sessionService,
            _navigationService,
            _popupService,
            _dispatcher,
            _activeProjectService);
    }

    public void Dispose()
    {
        // Dispose the SUT to unregister its WeakReferenceMessenger subscriptions,
        // preventing cross-test pollution. Guard against double-dispose for tests
        // that call _sut.Dispose() explicitly (e.g. Dispose_AfterDispose_*).
        if (!_sutDisposed)
            _sut.Dispose();
        // Also unregister any subscriptions registered by the test class itself.
        WeakReferenceMessenger.Default.UnregisterAll(this);
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

        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
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
        var act = () => new FlyoutViewModel(null!, _sessionService, _navigationService, _popupService, _dispatcher, _activeProjectService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("projectService");
    }

    [Fact]
    public void Constructor_WhenSessionServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FlyoutViewModel(_projectService, null!, _navigationService, _popupService, _dispatcher, _activeProjectService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("sessionService");
    }

    [Fact]
    public void Constructor_WhenNavigationServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FlyoutViewModel(_projectService, _sessionService, null!, _popupService, _dispatcher, _activeProjectService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("navigationService");
    }

    [Fact]
    public void Constructor_WhenPopupServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FlyoutViewModel(_projectService, _sessionService, _navigationService, null!, _dispatcher, _activeProjectService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("popupService");
    }

    [Fact]
    public void Constructor_WhenDispatcherIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FlyoutViewModel(_projectService, _sessionService, _navigationService, _popupService, null!, _activeProjectService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("dispatcher");
    }

    [Fact]
    public void Constructor_WhenActiveProjectServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FlyoutViewModel(
            _projectService, _sessionService, _navigationService,
            _popupService, _dispatcher, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("activeProjectService");
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

    // ─── LoadSessionsCommand — IsSelected based on CurrentSessionId ───────────

    [Fact]
    public async Task LoadSessionsCommand_WhenCurrentSessionIdIsSet_SetsIsSelectedOnMatchingSession()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-2";
        SetupLoadSessionsSuccess(sessions: BuildSessionList(3));

        // Act
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Assert
        _sut.Sessions.Single(s => s.Id == "sess-2").IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task LoadSessionsCommand_WhenCurrentSessionIdIsSet_DoesNotSetIsSelectedOnOtherSessions()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-2";
        SetupLoadSessionsSuccess(sessions: BuildSessionList(3));

        // Act
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Assert
        _sut.Sessions.Where(s => s.Id != "sess-2").Should().AllSatisfy(s => s.IsSelected.Should().BeFalse());
    }

    [Fact]
    public async Task LoadSessionsCommand_WhenCurrentSessionIdIsNull_AllSessionsHaveIsSelectedFalse()
    {
        // Arrange
        _sut.CurrentSessionId = null;
        SetupLoadSessionsSuccess(sessions: BuildSessionList(3));

        // Act
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Assert
        _sut.Sessions.Should().AllSatisfy(s => s.IsSelected.Should().BeFalse());
    }

    // ─── LoadSessionsCommand — error path [REQ-034] ──────────────────────────

    [Fact]
    public async Task LoadSessionsCommand_WhenProjectServiceThrows_ClearsSessions()
    {
        // Arrange
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
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
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
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
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
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
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
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
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
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
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
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
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
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
        await _activeProjectService.DidNotReceive().GetActiveProjectAsync(Arg.Any<CancellationToken>());
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

    // ─── SelectSessionCommand — different session navigates [REQ-037] ─────────

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

    // ─── SelectSessionCommand — already-active session guard (REQ-005 / AC-004) ─

    [Fact]
    public async Task SelectSessionCommand_WhenSessionIsAlreadyActive_DoesNotNavigate()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";

        // Act
        await _sut.SelectSessionCommand.ExecuteAsync("sess-1");

        // Assert
        await _navigationService.DidNotReceive().GoToAsync(
            Arg.Any<string>(),
            Arg.Any<IDictionary<string, object>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectSessionCommand_WhenSessionIsAlreadyActive_CallsCloseFlyoutAsync()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";

        // Act
        await _sut.SelectSessionCommand.ExecuteAsync("sess-1");

        // Assert
        await _navigationService.Received(1).CloseFlyoutAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectSessionCommand_WhenSessionIsDifferentFromActive_NavigatesToChat()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";

        // Act
        await _sut.SelectSessionCommand.ExecuteAsync("sess-2");

        // Assert
        await _navigationService.Received(1).GoToAsync(
            "//chat",
            Arg.Is<IDictionary<string, object>>(d => d["sessionId"].Equals("sess-2")),
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

    // ─── CurrentSessionChangedMessage — updates IsSelected on sessions ─────────
    // The CurrentSessionChangedMessage handler in FlyoutViewModel is synchronous.
    // No Task.Delay is needed — the handler runs inline when Send() is called.

    [Fact]
    public async Task CurrentSessionChangedMessage_WhenReceived_SetsCurrentSessionId()
    {
        // Arrange
        SetupLoadSessionsSuccess(sessions: BuildSessionList(3));
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Act — handler is synchronous; CurrentSessionId is updated before Send() returns
        WeakReferenceMessenger.Default.Send(new CurrentSessionChangedMessage("sess-2"));

        // Assert
        _sut.CurrentSessionId.Should().Be("sess-2");
    }

    [Fact]
    public async Task CurrentSessionChangedMessage_WhenReceived_SetsIsSelectedOnMatchingSession()
    {
        // Arrange
        SetupLoadSessionsSuccess(sessions: BuildSessionList(3));
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Act — handler is synchronous; Sessions is updated before Send() returns
        WeakReferenceMessenger.Default.Send(new CurrentSessionChangedMessage("sess-2"));

        // Assert
        _sut.Sessions.Single(s => s.Id == "sess-2").IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task CurrentSessionChangedMessage_WhenReceived_ClearsIsSelectedOnNonMatchingSession()
    {
        // Arrange
        SetupLoadSessionsSuccess(sessions: BuildSessionList(3));
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Act — handler is synchronous; Sessions is updated before Send() returns
        WeakReferenceMessenger.Default.Send(new CurrentSessionChangedMessage("sess-2"));

        // Assert
        _sut.Sessions.Where(s => s.Id != "sess-2").Should().AllSatisfy(s => s.IsSelected.Should().BeFalse());
    }

    [Fact]
    public async Task CurrentSessionChangedMessage_WhenSessionIdIsNull_ClearsAllIsSelected()
    {
        // Arrange — load sessions and mark sess-1 as selected via the messenger
        SetupLoadSessionsSuccess(sessions: BuildSessionList(3));
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Both sends are synchronous — the handler runs inline before Send() returns.
        // No Task.Delay between sends avoids yielding the thread to other async operations.
        WeakReferenceMessenger.Default.Send(new CurrentSessionChangedMessage("sess-1"));

        // Act — send null to clear all selections (synchronous handler)
        WeakReferenceMessenger.Default.Send(new CurrentSessionChangedMessage(null));

        // Assert — no delay needed; handler is synchronous
        _sut.Sessions.Should().AllSatisfy(s => s.IsSelected.Should().BeFalse());
    }

    // ─── SessionDeletedMessage — triggers reload ───────────────────────────────

    [Fact]
    public async Task SessionDeletedMessage_WhenReceived_ReloadsSessionList()
    {
        // Arrange
        SetupLoadSessionsSuccess();
        await _sut.LoadSessionsCommand.ExecuteAsync(null);
        _activeProjectService.ClearReceivedCalls();

        // Act
        WeakReferenceMessenger.Default.Send(new SessionDeletedMessage("sess-1", "proj-1"));
        await Task.Delay(50); // allow fire-and-forget handler to complete

        // Assert
        await _activeProjectService.Received().GetActiveProjectAsync(Arg.Any<CancellationToken>());
    }

    // ─── IDisposable — Dispose unregisters messenger ──────────────────────────

    [Fact]
    public async Task Dispose_AfterDispose_DoesNotReceiveSessionDeletedMessage()
    {
        // Arrange
        SetupLoadSessionsSuccess();
        await _sut.LoadSessionsCommand.ExecuteAsync(null);
        _activeProjectService.ClearReceivedCalls();

        // Act — mark disposed so the test class Dispose() does not call it again
        _sutDisposed = true;
        _sut.Dispose();
        WeakReferenceMessenger.Default.Send(new SessionDeletedMessage("sess-1", "proj-1"));
        await Task.Delay(50);

        // Assert — LoadSessions should NOT have been triggered after Dispose
        await _activeProjectService.DidNotReceive().GetActiveProjectAsync(Arg.Any<CancellationToken>());
    }

    // ─── NewSessionCommand — happy path, active project exists [REQ-002, AC-001] ─

    [Fact]
    public async Task NewSessionCommand_WhenActiveProjectExists_CreatesSessionAndNavigatesToChat()
    {
        // Arrange
        var project = BuildProject(id: "proj-1");
        var session = BuildSession(id: "new-sess-1", projectId: "proj-1");
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns(project);
        _sessionService.CreateSessionForProjectAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        await _sut.NewSessionCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.Received(1).GoToAsync(
            "//chat",
            Arg.Is<IDictionary<string, object>>(d => d["sessionId"].Equals("new-sess-1")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewSessionCommand_WhenActiveProjectExists_PrependsNewSessionToList()
    {
        // Arrange
        var project = BuildProject(id: "proj-1");
        var existingSessions = BuildSessionList(2, projectId: "proj-1");
        var newSession = BuildSession(id: "new-sess-99", projectId: "proj-1", title: "Brand New");

        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns(project);
        _sessionService.GetSessionsByProjectAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(existingSessions);
        _sessionService.CreateSessionForProjectAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(newSession);

        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Act
        await _sut.NewSessionCommand.ExecuteAsync(null);

        // Assert
        _sut.Sessions[0].Id.Should().Be("new-sess-99");
    }

    [Fact]
    public async Task NewSessionCommand_WhenActiveProjectExists_ClearsCreationError()
    {
        // Arrange
        var project = BuildProject(id: "proj-1");
        var session = BuildSession(id: "new-sess-1", projectId: "proj-1");
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns(project);
        _sessionService.CreateSessionForProjectAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(session);

        // Simulate a prior error
        _sut.CreationError = "Previous error";

        // Act
        await _sut.NewSessionCommand.ExecuteAsync(null);

        // Assert
        _sut.CreationError.Should().BeNull();
    }

    [Fact]
    public async Task NewSessionCommand_WhenActiveProjectExists_SetsIsCreatingSessionFalseAfterSuccess()
    {
        // Arrange
        var project = BuildProject(id: "proj-1");
        var session = BuildSession(id: "new-sess-1", projectId: "proj-1");
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns(project);
        _sessionService.CreateSessionForProjectAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        await _sut.NewSessionCommand.ExecuteAsync(null);

        // Assert
        _sut.IsCreatingSession.Should().BeFalse();
    }

    // ─── NewSessionCommand — no active project, projects available [REQ-003, AC-002] ─

    [Fact]
    public async Task NewSessionCommand_WhenNoActiveProject_FetchesProjectsAndActivatesFirst()
    {
        // Arrange
        var firstProject = BuildProject(id: "proj-first");
        var session = BuildSession(id: "new-sess-1", projectId: "proj-first");

        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null, firstProject);
        _activeProjectService.SetActiveProjectAsync("proj-first", Arg.Any<CancellationToken>())
            .Returns(true);
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProjectDto> { firstProject });
        _sessionService.CreateSessionForProjectAsync("proj-first", Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        await _sut.NewSessionCommand.ExecuteAsync(null);

        // Assert
        await _activeProjectService.Received(1).SetActiveProjectAsync("proj-first", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewSessionCommand_WhenNoActiveProject_CreatesSessionAfterActivatingProject()
    {
        // Arrange
        var firstProject = BuildProject(id: "proj-first");
        var session = BuildSession(id: "new-sess-1", projectId: "proj-first");

        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null, firstProject);
        _activeProjectService.SetActiveProjectAsync("proj-first", Arg.Any<CancellationToken>())
            .Returns(true);
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProjectDto> { firstProject });
        _sessionService.CreateSessionForProjectAsync("proj-first", Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        await _sut.NewSessionCommand.ExecuteAsync(null);

        // Assert
        await _sessionService.Received(1).CreateSessionForProjectAsync("proj-first", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewSessionCommand_WhenNoActiveProject_NavigatesToChat()
    {
        // Arrange
        var firstProject = BuildProject(id: "proj-first");
        var session = BuildSession(id: "new-sess-1", projectId: "proj-first");

        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null, firstProject);
        _activeProjectService.SetActiveProjectAsync("proj-first", Arg.Any<CancellationToken>())
            .Returns(true);
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProjectDto> { firstProject });
        _sessionService.CreateSessionForProjectAsync("proj-first", Arg.Any<CancellationToken>())
            .Returns(session);

        // Act
        await _sut.NewSessionCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.Received(1).GoToAsync(
            "//chat",
            Arg.Is<IDictionary<string, object>>(d => d["sessionId"].Equals("new-sess-1")),
            Arg.Any<CancellationToken>());
    }

    // ─── NewSessionCommand — no active project, no projects available [REQ-003, AC-003] ─

    [Fact]
    public async Task NewSessionCommand_WhenNoProjectsAvailable_SetsCreationError()
    {
        // Arrange
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProjectDto>());

        // Act
        await _sut.NewSessionCommand.ExecuteAsync(null);

        // Assert
        _sut.CreationError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task NewSessionCommand_WhenNoProjectsAvailable_DoesNotNavigate()
    {
        // Arrange
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProjectDto>());

        // Act
        await _sut.NewSessionCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.DidNotReceive().GoToAsync(
            Arg.Any<string>(),
            Arg.Any<IDictionary<string, object>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewSessionCommand_WhenNoProjectsAvailable_SetsIsCreatingSessionFalse()
    {
        // Arrange
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProjectDto>());

        // Act
        await _sut.NewSessionCommand.ExecuteAsync(null);

        // Assert
        _sut.IsCreatingSession.Should().BeFalse();
    }

    // ─── NewSessionCommand — SetActiveProjectAsync returns false ──────────────

    [Fact]
    public async Task NewSessionCommand_WhenSetActiveProjectFails_SetsCreationError()
    {
        // Arrange
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { BuildProject() }.ToList().AsReadOnly() as IReadOnlyList<ProjectDto>);
        _activeProjectService.SetActiveProjectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.NewSessionCommand.ExecuteAsync(null);

        // Assert
        _sut.CreationError.Should().Be("Failed to activate project. Please try again.");
    }

    [Fact]
    public async Task NewSessionCommand_WhenSetActiveProjectFails_DoesNotNavigate()
    {
        // Arrange
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { BuildProject() }.ToList().AsReadOnly() as IReadOnlyList<ProjectDto>);
        _activeProjectService.SetActiveProjectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.NewSessionCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.DidNotReceive().GoToAsync(
            Arg.Any<string>(),
            Arg.Any<IDictionary<string, object>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewSessionCommand_WhenSetActiveProjectFails_SetsIsCreatingSessionFalse()
    {
        // Arrange
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);
        _projectService.GetAllProjectsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { BuildProject() }.ToList().AsReadOnly() as IReadOnlyList<ProjectDto>);
        _activeProjectService.SetActiveProjectAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.NewSessionCommand.ExecuteAsync(null);

        // Assert
        _sut.IsCreatingSession.Should().BeFalse();
    }

    // ─── NewSessionCommand — creation API fails [REQ-009, AC-005] ────────────

    [Fact]
    public async Task NewSessionCommand_WhenCreateSessionThrows_SetsCreationError()
    {
        // Arrange
        var project = BuildProject(id: "proj-1");
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns(project);
        _sessionService.CreateSessionForProjectAsync("proj-1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Failed to create session: Service unavailable"));

        // Act
        await _sut.NewSessionCommand.ExecuteAsync(null);

        // Assert
        _sut.CreationError.Should().Be("Failed to create session: Service unavailable");
    }

    [Fact]
    public async Task NewSessionCommand_WhenCreateSessionThrows_DoesNotNavigate()
    {
        // Arrange
        var project = BuildProject(id: "proj-1");
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns(project);
        _sessionService.CreateSessionForProjectAsync("proj-1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Failed to create session: Service unavailable"));

        // Act
        await _sut.NewSessionCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.DidNotReceive().GoToAsync(
            Arg.Any<string>(),
            Arg.Any<IDictionary<string, object>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NewSessionCommand_WhenCreateSessionThrows_SetsIsCreatingSessionFalse()
    {
        // Arrange
        var project = BuildProject(id: "proj-1");
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns(project);
        _sessionService.CreateSessionForProjectAsync("proj-1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Failed to create session: Service unavailable"));

        // Act
        await _sut.NewSessionCommand.ExecuteAsync(null);

        // Assert
        _sut.IsCreatingSession.Should().BeFalse();
    }

    // ─── NewSessionCommand — loading state [REQ-008, AC-004] ─────────────────

    [Fact]
    public async Task NewSessionCommand_SetsIsCreatingSessionTrueWhileExecuting()
    {
        // Arrange
        var project = BuildProject(id: "proj-1");
        var session = BuildSession(id: "new-sess-1", projectId: "proj-1");
        var isCreatingDuringCall = false;

        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns(project);
        _sessionService.CreateSessionForProjectAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                isCreatingDuringCall = _sut.IsCreatingSession;
                return Task.FromResult(session);
            });

        // Act
        await _sut.NewSessionCommand.ExecuteAsync(null);

        // Assert
        isCreatingDuringCall.Should().BeTrue();
        _sut.IsCreatingSession.Should().BeFalse();
    }

    [Fact]
    public async Task NewSessionCommand_ClearsCreationErrorAtStart()
    {
        // Arrange
        var project = BuildProject(id: "proj-1");
        var session = BuildSession(id: "new-sess-1", projectId: "proj-1");
        string? creationErrorDuringCall = "not-cleared";

        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns(project);
        _sessionService.CreateSessionForProjectAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                creationErrorDuringCall = _sut.CreationError;
                return Task.FromResult(session);
            });

        // Simulate a prior error
        _sut.CreationError = "Previous error";

        // Act
        await _sut.NewSessionCommand.ExecuteAsync(null);

        // Assert — CreationError was null at the start of execution (before the service call)
        creationErrorDuringCall.Should().BeNull();
    }

    // ─── SessionTitleUpdatedMessage — drawer title update [REQ-010, AC-007] ───

    [Fact]
    public async Task SessionTitleUpdatedMessage_WhenReceived_UpdatesMatchingSessionTitle()
    {
        // Arrange — populate the session list first
        SetupLoadSessionsSuccess(sessions: BuildSessionList(3));
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Act — send the title update message (handler is synchronous via _dispatcher mock)
        WeakReferenceMessenger.Default.Send(new SessionTitleUpdatedMessage("sess-2", "Updated Title"));

        // Assert
        _sut.Sessions.Single(s => s.Id == "sess-2").Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task SessionTitleUpdatedMessage_WhenSessionIdDoesNotMatch_DoesNotUpdateSessions()
    {
        // Arrange — populate the session list first
        var sessions = BuildSessionList(3);
        SetupLoadSessionsSuccess(sessions: sessions);
        await _sut.LoadSessionsCommand.ExecuteAsync(null);

        // Capture original titles
        var originalTitles = _sut.Sessions.Select(s => s.Title).ToList();

        // Act — send a message for a session ID that does not exist in the list
        WeakReferenceMessenger.Default.Send(new SessionTitleUpdatedMessage("sess-999", "Should Not Apply"));

        // Assert — all titles remain unchanged
        _sut.Sessions.Select(s => s.Title).Should().BeEquivalentTo(originalTitles);
    }
}
