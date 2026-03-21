using NSubstitute.ExceptionExtensions;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="SplashViewModel"/>.
/// </summary>
public sealed class SplashViewModelTests
{
    private readonly IServerConnectionRepository _serverConnectionRepo;
    private readonly IOpencodeConnectionManager _connectionManager;
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly SplashViewModel _sut;

    /// <summary>
    /// A <see cref="TimeProvider"/> that fires <see cref="Task.Delay"/> timers immediately,
    /// preventing real 2-second waits in unit tests.
    /// </summary>
    private sealed class InstantTimeProvider : TimeProvider
    {
        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            // Fire the callback immediately so Task.Delay returns without waiting.
            callback(state);
            // Return a no-op timer that never fires again.
            return base.CreateTimer(_ => { }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// A <see cref="TimeProvider"/> that fires timers immediately AND records the
    /// <c>dueTime</c> passed to <see cref="CreateTimer"/> for assertion in tests.
    /// </summary>
    private sealed class SpyTimeProvider : TimeProvider
    {
        /// <summary>Gets the most recent <c>dueTime</c> passed to <see cref="CreateTimer"/>.</summary>
        public TimeSpan LastDueTime { get; private set; }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            LastDueTime = dueTime;
            // Fire the callback immediately so Task.Delay returns without waiting.
            callback(state);
            // Return a no-op timer that never fires again.
            return base.CreateTimer(_ => { }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    public SplashViewModelTests()
    {
        _serverConnectionRepo = Substitute.For<IServerConnectionRepository>();
        _connectionManager = Substitute.For<IOpencodeConnectionManager>();
        _sessionService = Substitute.For<ISessionService>();
        _navigationService = Substitute.For<INavigationService>();

        _sut = new SplashViewModel(
            _serverConnectionRepo,
            _connectionManager,
            _sessionService,
            _navigationService,
            timeProvider: new InstantTimeProvider());
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private static ServerConnectionDto BuildConnection(string id = "conn-1")
        => new(id, "Test Server", "192.168.1.100", 4096, "opencode", true, false, false,
               DateTime.UtcNow, DateTime.UtcNow, false);

    private static openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto BuildSession(
        string id = "sess-1",
        long updated = 1710000001000)
    {
        var time = new SessionTimeDto(Created: 1710000000000, Updated: updated, Compacting: null);
        return new openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto(
            Id: id, ProjectId: "proj-1", Directory: "/path", ParentId: null,
            Summary: null, Share: null, Title: "Test", Version: "1",
            Time: time, Revert: null);
    }

    // ─── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesWithIsLoadingFalse()
    {
        // Assert
        _sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void Constructor_InitializesWithStatusMessageEmpty()
    {
        // Assert
        _sut.StatusMessage.Should().BeEmpty();
    }

    // ─── InitializeAsync — No server configured ──────────────────────────────

    [Fact]
    public async Task InitializeCommand_WhenNoServerConfigured_NavigatesToOnboarding()
    {
        // Arrange
        _serverConnectionRepo.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns((ServerConnectionDto?)null);

        // Act
        await _sut.InitializeCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.Received(1).GoToAsync("//onboarding", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeCommand_WhenNoServerConfigured_DoesNotCheckReachability()
    {
        // Arrange
        _serverConnectionRepo.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns((ServerConnectionDto?)null);

        // Act
        await _sut.InitializeCommand.ExecuteAsync(null);

        // Assert
        await _connectionManager.DidNotReceive().IsServerReachableAsync(Arg.Any<CancellationToken>());
    }

    // ─── InitializeAsync — Server not reachable ──────────────────────────────

    [Fact]
    public async Task InitializeCommand_WhenServerNotReachable_NavigatesToServerManagement()
    {
        // Arrange
        _serverConnectionRepo.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(BuildConnection());
        _connectionManager.IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.InitializeCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.Received(1).GoToAsync("//server-management", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeCommand_WhenServerNotReachable_SetsStatusMessageToConnectionError()
    {
        // Arrange
        _serverConnectionRepo.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(BuildConnection());
        _connectionManager.IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.InitializeCommand.ExecuteAsync(null);

        // Assert
        _sut.StatusMessage.Should().Be("Errore di connessione al server");
    }

    [Fact]
    public async Task InitializeCommand_WhenServerNotReachable_DoesNotLoadSessions()
    {
        // Arrange
        _serverConnectionRepo.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(BuildConnection());
        _connectionManager.IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.InitializeCommand.ExecuteAsync(null);

        // Assert
        await _sessionService.DidNotReceive().GetAllSessionsAsync(Arg.Any<CancellationToken>());
    }

    // ─── InitializeAsync — Server reachable with sessions ────────────────────

    [Fact]
    public async Task InitializeCommand_WhenServerReachableWithSessions_NavigatesToChatWithSessionId()
    {
        // Arrange
        _serverConnectionRepo.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(BuildConnection());
        _connectionManager.IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        var sessions = new List<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>
        {
            BuildSession("sess-old", updated: 1000),
            BuildSession("sess-new", updated: 2000),
        };
        _sessionService.GetAllSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(sessions);

        // Act
        await _sut.InitializeCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.Received(1).GoToAsync(
            "//chat",
            Arg.Is<IDictionary<string, object>>(d => (string)d["sessionId"] == "sess-new"),
            Arg.Any<CancellationToken>());
    }

    // ─── InitializeAsync — Server reachable, no sessions ─────────────────────

    [Fact]
    public async Task InitializeCommand_WhenServerReachableNoSessions_NavigatesToChat()
    {
        // Arrange
        _serverConnectionRepo.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(BuildConnection());
        _connectionManager.IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(true);
        _sessionService.GetAllSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>());

        // Act
        await _sut.InitializeCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.Received(1).GoToAsync("//chat", Arg.Any<CancellationToken>());
    }

    // ─── InitializeAsync — IsLoading state ───────────────────────────────────

    [Fact]
    public async Task InitializeCommand_SetsIsLoadingFalseAfterCompletion()
    {
        // Arrange
        _serverConnectionRepo.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns((ServerConnectionDto?)null);

        // Act
        await _sut.InitializeCommand.ExecuteAsync(null);

        // Assert
        _sut.IsLoading.Should().BeFalse();
    }

    // ─── InitializeAsync — Exception fallback ────────────────────────────────

    [Fact]
    public async Task InitializeCommand_WhenUnexpectedExceptionOccurs_NavigatesToServerManagement()
    {
        // Arrange
        _serverConnectionRepo.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(BuildConnection());
        _connectionManager.IsServerReachableAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        // Act
        await _sut.InitializeCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.Received(1).GoToAsync("//server-management", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeCommand_WhenUnexpectedExceptionOccurs_SetsStatusMessageToConnectionError()
    {
        // Arrange
        _serverConnectionRepo.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(BuildConnection());
        _connectionManager.IsServerReachableAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        // Act
        await _sut.InitializeCommand.ExecuteAsync(null);

        // Assert
        _sut.StatusMessage.Should().Be("Errore di connessione al server");
    }

    [Fact]
    public async Task InitializeCommand_WhenUnexpectedExceptionOccurs_SetsIsLoadingFalse()
    {
        // Arrange
        _serverConnectionRepo.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(BuildConnection());
        _connectionManager.IsServerReachableAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        // Act
        await _sut.InitializeCommand.ExecuteAsync(null);

        // Assert
        _sut.IsLoading.Should().BeFalse();
    }

    // ─── InitializeAsync — Timeout treated as unreachable ────────────────────

    [Fact]
    public async Task InitializeCommand_WhenReachabilityCheckTimesOut_NavigatesToServerManagement()
    {
        // Arrange
        _serverConnectionRepo.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(BuildConnection());
        _connectionManager.IsServerReachableAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        await _sut.InitializeCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.Received(1).GoToAsync("//server-management", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeCommand_WhenReachabilityCheckTimesOut_SetsStatusMessageToUnreachable()
    {
        // Arrange
        _serverConnectionRepo.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(BuildConnection());
        _connectionManager.IsServerReachableAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        await _sut.InitializeCommand.ExecuteAsync(null);

        // Assert
        _sut.StatusMessage.Should().Be("Server non raggiungibile");
    }

    /// <summary>
    /// [m-004] Verifies that the 2-second delay duration is passed to <see cref="TimeProvider.CreateTimer"/>
    /// on the timeout path, so any accidental change to the delay value causes a test failure.
    /// </summary>
    [Fact]
    public async Task InitializeCommand_WhenReachabilityCheckTimesOut_DelayDurationIsTwoSeconds()
    {
        // Arrange
        var spy = new SpyTimeProvider();
        var sut = new SplashViewModel(
            _serverConnectionRepo,
            _connectionManager,
            _sessionService,
            _navigationService,
            timeProvider: spy);

        _serverConnectionRepo.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(BuildConnection());
        _connectionManager.IsServerReachableAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        await sut.InitializeCommand.ExecuteAsync(null);

        // Assert
        spy.LastDueTime.Should().Be(TimeSpan.FromSeconds(2));
    }

    // ─── InitializeAsync — Outer token cancelled (app shutdown) ──────────────

    /// <summary>
    /// [M-001] Verifies the symmetric negative case: when the outer <see cref="CancellationToken"/>
    /// is cancelled (app shutdown / page dismissed), an <see cref="OperationCanceledException"/>
    /// thrown by the reachability check must be silently swallowed and navigation must NOT occur.
    /// <para>
    /// The guard <c>catch (OperationCanceledException) when (!ct.IsCancellationRequested)</c>
    /// only fires for the timeout path. When <c>ct.IsCancellationRequested</c> is <c>true</c>,
    /// the outer <c>catch (OperationCanceledException) when (ct.IsCancellationRequested)</c>
    /// fires instead and does nothing.
    /// </para>
    /// </summary>
    [Fact]
    public async Task InitializeCommand_WhenOuterTokenCancelledDuringReachabilityCheck_DoesNotNavigate()
    {
        // Arrange
        _serverConnectionRepo.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(BuildConnection());

        // Make IsServerReachableAsync cancel the command's own token (simulating app shutdown)
        // then throw OperationCanceledException. This causes ct.IsCancellationRequested == true
        // inside InitializeAsync, so the outer "app shutdown" catch fires — not the timeout path.
        _connectionManager.IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                _sut.InitializeCommand.Cancel();
                await Task.Yield();
                throw new OperationCanceledException();
#pragma warning disable CS0162 // Unreachable code — required to satisfy bool return type
                return false;
#pragma warning restore CS0162
            });

        // Act
        await _sut.InitializeCommand.ExecuteAsync(null);

        // Assert — the app-shutdown path must swallow the exception and never navigate
        await _navigationService.DidNotReceive().GoToAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
