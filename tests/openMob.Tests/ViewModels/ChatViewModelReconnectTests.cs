using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Models;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for the pending-permission replay logic introduced in
/// <see cref="ChatViewModel"/> for the Auto-Accept Reconnect feature.
/// Covers <c>ReplayPendingPermissionsAsync</c>, <c>_previousHealthState</c> tracking,
/// and the extended <c>OnHealthStateChanged</c> handler.
/// </summary>
[Collection(MessengerTestCollection.Name)]
public sealed class ChatViewModelReconnectTests : IDisposable
{
    private readonly IProjectService _projectService;
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;
    private readonly IOpencodeConnectionManager _connectionManager;
    private readonly IProjectPreferenceService _preferenceService;
    private readonly IChatService _chatService;
    private readonly IOpencodeApiClient _apiClient;
    private readonly IDispatcherService _dispatcher;
    private readonly IActiveProjectService _activeProjectService;
    private readonly IHeartbeatMonitorService _heartbeatMonitor;
    private readonly ChatViewModel _sut;

    public ChatViewModelReconnectTests()
    {
        _projectService = Substitute.For<IProjectService>();
        _sessionService = Substitute.For<ISessionService>();
        _navigationService = Substitute.For<INavigationService>();
        _popupService = Substitute.For<IAppPopupService>();
        _connectionManager = Substitute.For<IOpencodeConnectionManager>();
        _preferenceService = Substitute.For<IProjectPreferenceService>();
        _chatService = Substitute.For<IChatService>();
        _apiClient = Substitute.For<IOpencodeApiClient>();
        _dispatcher = Substitute.For<IDispatcherService>();
        _activeProjectService = Substitute.For<IActiveProjectService>();
        _heartbeatMonitor = Substitute.For<IHeartbeatMonitorService>();

        // CRITICAL: IDispatcherService mock must execute the action synchronously
        _dispatcher.When(d => d.Dispatch(Arg.Any<Action>())).Do(ci => ci.Arg<Action>()());

        // Default: server connected
        _connectionManager.ConnectionStatus.Returns(ServerConnectionStatus.Connected);

        // Default: GetMessagesAsync returns empty success so SetSession / LoadMessages does not error
        _chatService
            .GetMessagesAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Ok(new List<MessageWithPartsDto>()));

        // Default: SubscribeToEventsAsync returns an empty async enumerable so SSE does not block
        _chatService
            .SubscribeToEventsAsync(Arg.Any<CancellationToken>())
            .Returns(EmptyEvents());

        _sut = new ChatViewModel(
            _projectService,
            _sessionService,
            _navigationService,
            _popupService,
            _connectionManager,
            _preferenceService,
            _chatService,
            _apiClient,
            _dispatcher,
            _activeProjectService,
            _heartbeatMonitor);
    }

    public void Dispose() => _sut.Dispose();

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Returns an empty async enumerable for the SSE subscription default stub.</summary>
    private static async IAsyncEnumerable<ChatEvent> EmptyEvents()
    {
        await Task.CompletedTask;
        yield break;
    }

    /// <summary>
    /// Builds a <see cref="PermissionRequestDto"/> with sensible defaults.
    /// </summary>
    private static PermissionRequestDto BuildPermissionRequestDto(
        string id = "perm-1",
        string sessionId = "sess-1",
        string permission = "edit",
        string[]? patterns = null,
        string[]? always = null)
        => new(
            Id: id,
            SessionId: sessionId,
            Permission: permission,
            Patterns: patterns ?? ["/path/to/file"],
            Always: always ?? ["/path/to/file"]);

    /// <summary>
    /// Fires a <see cref="ConnectionHealthState"/> transition via the heartbeat monitor event.
    /// </summary>
    private void FireHealthState(ConnectionHealthState state)
    {
        _heartbeatMonitor.HealthStateChanged += Raise.Event<Action<ConnectionHealthState>>(state);
    }

    // ─── AC-001 — Replay fires on Lost → Healthy with AutoAccept = true ───────

    [Fact]
    public async Task ReplayPendingPermissions_WhenAutoAcceptTrueAndTransitionFromLostToHealthy_CallsGetPendingPermissionsAndReplies()
    {
        // Arrange
        _sut.AutoAccept = true;
        _sut.CurrentSessionId = "sess-1";

        var permission = BuildPermissionRequestDto(id: "perm-1", sessionId: "sess-1");
        _apiClient
            .GetPendingPermissionsAsync("sess-1", Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<PermissionRequestDto>>.Success(
                new List<PermissionRequestDto> { permission }));

        _apiClient
            .ReplyToPermissionAsync("perm-1", "always", Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<bool>.Success(true));

        // Act — fire Lost first to set _previousHealthState = Lost, then Healthy to trigger replay
        FireHealthState(ConnectionHealthState.Lost);
        FireHealthState(ConnectionHealthState.Healthy);

        // Wait for the fire-and-forget ReplayPendingPermissionsAsync to complete
        await Task.Delay(200);

        // Assert
        await _apiClient.Received(1).GetPendingPermissionsAsync("sess-1", Arg.Any<CancellationToken>());
        await _apiClient.Received(1).ReplyToPermissionAsync("perm-1", "always", Arg.Any<CancellationToken>());
    }

    // ─── AC-002 — No replay when AutoAccept = false ───────────────────────────

    [Fact]
    public async Task ReplayPendingPermissions_WhenAutoAcceptFalse_DoesNotCallGetPendingPermissions()
    {
        // Arrange
        _sut.AutoAccept = false;
        _sut.CurrentSessionId = "sess-1";

        // Act — fire Lost then Healthy
        FireHealthState(ConnectionHealthState.Lost);
        FireHealthState(ConnectionHealthState.Healthy);

        await Task.Delay(100);

        // Assert
        await _apiClient.DidNotReceive().GetPendingPermissionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ─── AC-003 — No replay on repeated Healthy (no state change) ─────────────

    [Fact]
    public async Task ReplayPendingPermissions_WhenAlreadyHealthyAndHealthyAgain_DoesNotCallGetPendingPermissions()
    {
        // Arrange — AutoAccept is true but _previousHealthState starts as Healthy (the default)
        _sut.AutoAccept = true;
        _sut.CurrentSessionId = "sess-1";

        // Act — fire Healthy without a prior Lost/Degraded (Healthy → Healthy, no transition)
        FireHealthState(ConnectionHealthState.Healthy);

        await Task.Delay(100);

        // Assert — no replay because previous state was already Healthy
        await _apiClient.DidNotReceive().GetPendingPermissionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ─── AC-004 — Two permissions replied sequentially ────────────────────────

    [Fact]
    public async Task ReplayPendingPermissions_WhenTwoPendingPermissions_RepliesBothSequentially()
    {
        // Arrange
        _sut.AutoAccept = true;
        _sut.CurrentSessionId = "sess-1";

        var perm1 = BuildPermissionRequestDto(id: "perm-1", sessionId: "sess-1");
        var perm2 = BuildPermissionRequestDto(id: "perm-2", sessionId: "sess-1");

        _apiClient
            .GetPendingPermissionsAsync("sess-1", Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<PermissionRequestDto>>.Success(
                new List<PermissionRequestDto> { perm1, perm2 }));

        _apiClient
            .ReplyToPermissionAsync(Arg.Any<string>(), "always", Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<bool>.Success(true));

        // Act
        FireHealthState(ConnectionHealthState.Lost);
        FireHealthState(ConnectionHealthState.Healthy);

        await Task.Delay(200);

        // Assert — both permissions replied with "always"
        await _apiClient.Received(1).ReplyToPermissionAsync("perm-1", "always", Arg.Any<CancellationToken>());
        await _apiClient.Received(1).ReplyToPermissionAsync("perm-2", "always", Arg.Any<CancellationToken>());
    }

    // ─── AC-005 — Sentry capture on GetPendingPermissionsAsync failure ─────────

    [Fact]
    public async Task ReplayPendingPermissions_WhenGetPendingPermissionsFails_DoesNotCallReplyToPermission()
    {
        // Arrange
        _sut.AutoAccept = true;
        _sut.CurrentSessionId = "sess-1";

        _apiClient
            .GetPendingPermissionsAsync("sess-1", Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<PermissionRequestDto>>.Failure(
                new OpencodeApiError(ErrorKind.NetworkUnreachable, "Network error", null, null)));

        // Act
        FireHealthState(ConnectionHealthState.Lost);
        FireHealthState(ConnectionHealthState.Healthy);

        await Task.Delay(200);

        // Assert — fetch failed, so no reply should be attempted
        await _apiClient.DidNotReceive().ReplyToPermissionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ─── AC-006 — Fail-and-continue when first reply fails ────────────────────

    [Fact]
    public async Task ReplayPendingPermissions_WhenFirstReplyFails_ContinuesToSecondPermission()
    {
        // Arrange
        _sut.AutoAccept = true;
        _sut.CurrentSessionId = "sess-1";

        var perm1 = BuildPermissionRequestDto(id: "perm-1", sessionId: "sess-1");
        var perm2 = BuildPermissionRequestDto(id: "perm-2", sessionId: "sess-1");

        _apiClient
            .GetPendingPermissionsAsync("sess-1", Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<PermissionRequestDto>>.Success(
                new List<PermissionRequestDto> { perm1, perm2 }));

        // First reply throws; second reply succeeds
        _apiClient
            .ReplyToPermissionAsync("perm-1", "always", Arg.Any<CancellationToken>())
            .Returns<Task<OpencodeResult<bool>>>(_ => throw new HttpRequestException("network error"));

        _apiClient
            .ReplyToPermissionAsync("perm-2", "always", Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<bool>.Success(true));

        // Act
        FireHealthState(ConnectionHealthState.Lost);
        FireHealthState(ConnectionHealthState.Healthy);

        await Task.Delay(200);

        // Assert — second permission was still replied despite first failing
        await _apiClient.Received(1).ReplyToPermissionAsync("perm-2", "always", Arg.Any<CancellationToken>());
    }

    // ─── AC-008 — Duplicate guard via _inFlightPermissionReplies ──────────────

    [Fact]
    public async Task ReplayPendingPermissions_WhenPermissionAlreadyInFlight_DoesNotMakeDuplicateApiCall()
    {
        // Arrange
        _sut.AutoAccept = true;
        _sut.CurrentSessionId = "sess-1";

        var perm1 = BuildPermissionRequestDto(id: "perm-1", sessionId: "sess-1");

        _apiClient
            .GetPendingPermissionsAsync("sess-1", Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<PermissionRequestDto>>.Success(
                new List<PermissionRequestDto> { perm1 }));

        // Configure the first reply to delay so it is still in-flight when the second replay fires
        var firstCallTcs = new TaskCompletionSource<OpencodeResult<bool>>();
        var callCount = 0;

        _apiClient
            .ReplyToPermissionAsync("perm-1", "always", Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                    return firstCallTcs.Task;

                return Task.FromResult(OpencodeResult<bool>.Success(true));
            });

        // Act — first replay starts, first call is in-flight
        FireHealthState(ConnectionHealthState.Lost);
        FireHealthState(ConnectionHealthState.Healthy);

        // Give the first replay time to start and enter the in-flight state
        await Task.Delay(50);

        // Second replay fires while first is still in-flight
        FireHealthState(ConnectionHealthState.Degraded);
        FireHealthState(ConnectionHealthState.Healthy);

        // Give the second replay time to attempt (and be blocked by the guard)
        await Task.Delay(50);

        // Complete the first call
        firstCallTcs.SetResult(OpencodeResult<bool>.Success(true));

        // Wait for everything to settle
        await Task.Delay(200);

        // Assert — ReplyToPermissionAsync called only once despite two replays
        await _apiClient.Received(1).ReplyToPermissionAsync("perm-1", "always", Arg.Any<CancellationToken>());
    }

    // ─── Additional edge cases ─────────────────────────────────────────────────

    [Fact]
    public async Task ReplayPendingPermissions_WhenCurrentSessionIdIsNull_DoesNotCallGetPendingPermissions()
    {
        // Arrange — AutoAccept is true but no session is set
        _sut.AutoAccept = true;
        // CurrentSessionId is null by default

        // Act
        FireHealthState(ConnectionHealthState.Lost);
        FireHealthState(ConnectionHealthState.Healthy);

        await Task.Delay(100);

        // Assert — no session means no replay
        await _apiClient.DidNotReceive().GetPendingPermissionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplayPendingPermissions_WhenGetPendingPermissionsReturnsEmptyList_DoesNotCallReplyToPermission()
    {
        // Arrange
        _sut.AutoAccept = true;
        _sut.CurrentSessionId = "sess-1";

        _apiClient
            .GetPendingPermissionsAsync("sess-1", Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<PermissionRequestDto>>.Success(
                new List<PermissionRequestDto>()));

        // Act
        FireHealthState(ConnectionHealthState.Lost);
        FireHealthState(ConnectionHealthState.Healthy);

        await Task.Delay(100);

        // Assert — empty list means nothing to reply to
        await _apiClient.DidNotReceive().ReplyToPermissionAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplayPendingPermissions_WhenTransitionFromDegradedToHealthy_CallsGetPendingPermissions()
    {
        // Arrange — Degraded → Healthy should also trigger replay (previousState != Healthy)
        _sut.AutoAccept = true;
        _sut.CurrentSessionId = "sess-1";

        _apiClient
            .GetPendingPermissionsAsync("sess-1", Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<PermissionRequestDto>>.Success(
                new List<PermissionRequestDto>()));

        // Act — fire Degraded first (sets _previousHealthState = Degraded), then Healthy
        FireHealthState(ConnectionHealthState.Degraded);
        FireHealthState(ConnectionHealthState.Healthy);

        await Task.Delay(100);

        // Assert — Degraded → Healthy is also a non-Healthy → Healthy transition
        await _apiClient.Received(1).GetPendingPermissionsAsync("sess-1", Arg.Any<CancellationToken>());
    }
}
