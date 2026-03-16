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
            _navigationService);
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
    public async Task InitializeCommand_WhenServerNotReachable_NavigatesToChat()
    {
        // Arrange
        _serverConnectionRepo.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(BuildConnection());
        _connectionManager.IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.InitializeCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.Received(1).GoToAsync("//chat", Arg.Any<CancellationToken>());
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
    public async Task InitializeCommand_WhenUnexpectedExceptionOccurs_NavigatesToChat()
    {
        // Arrange
        _serverConnectionRepo.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(BuildConnection());
        _connectionManager.IsServerReachableAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        // Act
        await _sut.InitializeCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.Received(1).GoToAsync("//chat", Arg.Any<CancellationToken>());
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
    public async Task InitializeCommand_WhenReachabilityCheckTimesOut_NavigatesToChat()
    {
        // Arrange
        _serverConnectionRepo.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(BuildConnection());
        _connectionManager.IsServerReachableAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        await _sut.InitializeCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.Received(1).GoToAsync("//chat", Arg.Any<CancellationToken>());
    }
}
