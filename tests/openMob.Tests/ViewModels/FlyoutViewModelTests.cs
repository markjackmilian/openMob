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
    private readonly FlyoutViewModel _sut;
    private bool _sutDisposed;

    public FlyoutViewModelTests()
    {
        _projectService = Substitute.For<IProjectService>();
        _sessionService = Substitute.For<ISessionService>();
        _navigationService = Substitute.For<INavigationService>();
        _popupService = Substitute.For<IAppPopupService>();
        _dispatcher = Substitute.For<IDispatcherService>();

        // CRITICAL: dispatcher must execute the action synchronously so that
        // Sessions assignments and CurrentSessionChangedMessage loop updates
        // are visible immediately after the awaited command or Send() call.
        _dispatcher.When(d => d.Dispatch(Arg.Any<Action>())).Do(ci => ci.Arg<Action>()());

        _sut = new FlyoutViewModel(
            _projectService,
            _sessionService,
            _navigationService,
            _popupService,
            _dispatcher);
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
        var act = () => new FlyoutViewModel(null!, _sessionService, _navigationService, _popupService, _dispatcher);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("projectService");
    }

    [Fact]
    public void Constructor_WhenSessionServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FlyoutViewModel(_projectService, null!, _navigationService, _popupService, _dispatcher);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("sessionService");
    }

    [Fact]
    public void Constructor_WhenNavigationServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FlyoutViewModel(_projectService, _sessionService, null!, _popupService, _dispatcher);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("navigationService");
    }

    [Fact]
    public void Constructor_WhenPopupServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FlyoutViewModel(_projectService, _sessionService, _navigationService, null!, _dispatcher);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("popupService");
    }

    [Fact]
    public void Constructor_WhenDispatcherIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new FlyoutViewModel(_projectService, _sessionService, _navigationService, _popupService, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("dispatcher");
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
        _projectService.ClearReceivedCalls();

        // Act
        WeakReferenceMessenger.Default.Send(new SessionDeletedMessage("sess-1", "proj-1"));
        await Task.Delay(50); // allow fire-and-forget handler to complete

        // Assert
        await _projectService.Received().GetCurrentProjectAsync(Arg.Any<CancellationToken>());
    }

    // ─── IDisposable — Dispose unregisters messenger ──────────────────────────

    [Fact]
    public async Task Dispose_AfterDispose_DoesNotReceiveSessionDeletedMessage()
    {
        // Arrange
        SetupLoadSessionsSuccess();
        await _sut.LoadSessionsCommand.ExecuteAsync(null);
        _projectService.ClearReceivedCalls();

        // Act — mark disposed so the test class Dispose() does not call it again
        _sutDisposed = true;
        _sut.Dispose();
        WeakReferenceMessenger.Default.Send(new SessionDeletedMessage("sess-1", "proj-1"));
        await Task.Delay(50);

        // Assert — LoadSessions should NOT have been triggered after Dispose
        await _projectService.DidNotReceive().GetCurrentProjectAsync(Arg.Any<CancellationToken>());
    }
}
