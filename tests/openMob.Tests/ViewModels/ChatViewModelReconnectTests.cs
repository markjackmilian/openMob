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
    /// Yields the given events in order, then completes.
    /// Used to inject SSE events through the chat service subscription.
    /// </summary>
    private static async IAsyncEnumerable<ChatEvent> YieldEvents(params ChatEvent[] events)
    {
        foreach (var e in events)
        {
            yield return e;
            await Task.Yield();
        }
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

        // TCS signals when ReplyToPermissionAsync is called — deterministic completion
        var replyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _apiClient
            .GetPendingPermissionsAsync("sess-1", Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<PermissionRequestDto>>.Success(
                new List<PermissionRequestDto> { permission }));

        _apiClient
            .ReplyToPermissionAsync("perm-1", "always", Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                replyTcs.TrySetResult(true);
                return Task.FromResult(OpencodeResult<bool>.Success(true));
            });

        // Act — fire Lost first to set _previousHealthState = Lost, then Healthy to trigger replay
        FireHealthState(ConnectionHealthState.Lost);
        FireHealthState(ConnectionHealthState.Healthy);

        // Wait deterministically for the reply call to complete (5 s timeout guards against hangs)
        await replyTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

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

        // Negative assertion: a short delay is acceptable here because we are asserting
        // that something did NOT happen. There is no signal to wait on.
        await Task.Delay(50);

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

        // Negative assertion: short delay is acceptable — no signal to wait on
        await Task.Delay(50);

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

        // TCS signals when the second (last) reply is made — both have been processed
        var secondReplyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _apiClient
            .ReplyToPermissionAsync("perm-1", "always", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(OpencodeResult<bool>.Success(true)));

        _apiClient
            .ReplyToPermissionAsync("perm-2", "always", Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                secondReplyTcs.TrySetResult(true);
                return Task.FromResult(OpencodeResult<bool>.Success(true));
            });

        // Act
        FireHealthState(ConnectionHealthState.Lost);
        FireHealthState(ConnectionHealthState.Healthy);

        await secondReplyTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

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

        // TCS signals when GetPendingPermissionsAsync is called so we know the replay ran
        var fetchTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _apiClient
            .GetPendingPermissionsAsync("sess-1", Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                fetchTcs.TrySetResult(true);
                return Task.FromResult(OpencodeResult<IReadOnlyList<PermissionRequestDto>>.Failure(
                    new OpencodeApiError(ErrorKind.NetworkUnreachable, "Network error", null, null)));
            });

        // Act
        FireHealthState(ConnectionHealthState.Lost);
        FireHealthState(ConnectionHealthState.Healthy);

        // Wait until the fetch was attempted, then assert no reply was made
        await fetchTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Small yield to allow any potential (erroneous) reply call to be scheduled
        await Task.Yield();

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

        // First reply throws; second reply succeeds and signals completion
        var secondReplyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _apiClient
            .ReplyToPermissionAsync("perm-1", "always", Arg.Any<CancellationToken>())
            .Returns<Task<OpencodeResult<bool>>>(_ => throw new HttpRequestException("network error"));

        _apiClient
            .ReplyToPermissionAsync("perm-2", "always", Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                secondReplyTcs.TrySetResult(true);
                return Task.FromResult(OpencodeResult<bool>.Success(true));
            });

        // Act
        FireHealthState(ConnectionHealthState.Lost);
        FireHealthState(ConnectionHealthState.Healthy);

        await secondReplyTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — second permission was still replied despite first failing
        await _apiClient.Received(1).ReplyToPermissionAsync("perm-2", "always", Arg.Any<CancellationToken>());
    }

    // ─── AC-007 — Permission card already in Messages transitions to Resolved ──

    [Fact]
    public async Task ReplayPendingPermissions_WhenPermissionCardAlreadyInMessages_CardTransitionsToResolved()
    {
        // Arrange — inject a permission card via SSE with AutoAccept=false so the card is rendered.
        // Do NOT pre-set CurrentSessionId — SetSession must be called to start the SSE subscription.
        _sut.AutoAccept = false;

        var permissionEvent = new PermissionRequestedEvent
        {
            Id = "perm-1",
            SessionId = "sess-1",
            Permission = "edit",
            Patterns = ["/path/to/file"],
            Metadata = new Dictionary<string, object>(),
            Always = ["/path/to/file"],
        };

        // TCS signals when the SSE permission event has been processed and the card added
        var cardAddedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Inject the card through the SSE stream so HandlePermissionRequested adds it to Messages
        // and increments _pendingPermissionCount via the normal code path
        _chatService
            .GetMessagesAsync("sess-1", Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<IReadOnlyList<MessageWithPartsDto>>.Ok(new List<MessageWithPartsDto>()));

        _chatService
            .SubscribeToEventsAsync(Arg.Any<CancellationToken>())
            .Returns(YieldEvents(permissionEvent));

        // SetSession starts the SSE subscription (CurrentSessionId was null, so the guard passes)
        _sut.SetSession("sess-1");

        // Wait for SSE to process the permission event and add the card
        await Task.Delay(100);

        // Verify the card was added before proceeding
        _sut.Messages.Should().ContainSingle(m => m.RequestId == "perm-1");
        _sut.HasPendingPermissions.Should().BeTrue();

        // Now switch AutoAccept on and stub the replay API calls
        _sut.AutoAccept = true;

        var permission = BuildPermissionRequestDto(id: "perm-1", sessionId: "sess-1");

        var replyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _apiClient
            .GetPendingPermissionsAsync("sess-1", Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<PermissionRequestDto>>.Success(
                new List<PermissionRequestDto> { permission }));

        _apiClient
            .ReplyToPermissionAsync("perm-1", "always", Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                replyTcs.TrySetResult(true);
                return Task.FromResult(OpencodeResult<bool>.Success(true));
            });

        // Act — trigger Lost → Healthy replay
        FireHealthState(ConnectionHealthState.Lost);
        FireHealthState(ConnectionHealthState.Healthy);

        await replyTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Small yield to allow the synchronous dispatcher to run ResolvePermissionRequest
        await Task.Yield();

        // Assert — the existing card in Messages is now Resolved
        var card = _sut.Messages.Single(m => m.RequestId == "perm-1");
        card.PermissionStatus.Should().Be(PermissionStatus.Resolved);
        card.ResolvedReply.Should().Be("always");
        card.ResolvedReplyLabel.Should().Be("Always");
        _sut.HasPendingPermissions.Should().BeFalse();
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

        // TCS controls when the first in-flight reply completes
        var firstCallBlockTcs = new TaskCompletionSource<OpencodeResult<bool>>(TaskCreationOptions.RunContinuationsAsynchronously);
        // TCS signals when GetPendingPermissionsAsync is called the second time (second replay started)
        var secondReplayStartedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;

        _apiClient
            .ReplyToPermissionAsync("perm-1", "always", Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                    return firstCallBlockTcs.Task;

                return Task.FromResult(OpencodeResult<bool>.Success(true));
            });

        // Signal when the second replay's GetPendingPermissionsAsync is called
        // (we need to know the second replay has started before we complete the first)
        var getPendingCallCount = 0;
        _apiClient
            .GetPendingPermissionsAsync("sess-1", Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                getPendingCallCount++;
                if (getPendingCallCount == 2)
                    secondReplayStartedTcs.TrySetResult(true);

                return Task.FromResult(OpencodeResult<IReadOnlyList<PermissionRequestDto>>.Success(
                    new List<PermissionRequestDto> { perm1 }));
            });

        // Act — first replay starts, first call is in-flight
        FireHealthState(ConnectionHealthState.Lost);
        FireHealthState(ConnectionHealthState.Healthy);

        // Give the first replay time to start and enter the in-flight state
        await Task.Delay(50);

        // Second replay fires while first is still in-flight
        FireHealthState(ConnectionHealthState.Degraded);
        FireHealthState(ConnectionHealthState.Healthy);

        // Wait for the second replay to reach GetPendingPermissionsAsync
        await secondReplayStartedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Complete the first call
        firstCallBlockTcs.SetResult(OpencodeResult<bool>.Success(true));

        // Wait for everything to settle
        await Task.Delay(100);

        // Assert — ReplyToPermissionAsync called only once despite two replays
        // (the second replay sees perm-1 still in _inFlightPermissionReplies and skips it)
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

        // Negative assertion: short delay is acceptable — no signal to wait on
        await Task.Delay(50);

        // Assert — no session means no replay
        await _apiClient.DidNotReceive().GetPendingPermissionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReplayPendingPermissions_WhenGetPendingPermissionsReturnsEmptyList_DoesNotCallReplyToPermission()
    {
        // Arrange
        _sut.AutoAccept = true;
        _sut.CurrentSessionId = "sess-1";

        // TCS signals when GetPendingPermissionsAsync is called so we know the replay ran
        var fetchTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _apiClient
            .GetPendingPermissionsAsync("sess-1", Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                fetchTcs.TrySetResult(true);
                return Task.FromResult(OpencodeResult<IReadOnlyList<PermissionRequestDto>>.Success(
                    new List<PermissionRequestDto>()));
            });

        // Act
        FireHealthState(ConnectionHealthState.Lost);
        FireHealthState(ConnectionHealthState.Healthy);

        await fetchTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Small yield to allow any potential (erroneous) reply call to be scheduled
        await Task.Yield();

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

        // TCS signals when GetPendingPermissionsAsync is called
        var fetchTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _apiClient
            .GetPendingPermissionsAsync("sess-1", Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                fetchTcs.TrySetResult(true);
                return Task.FromResult(OpencodeResult<IReadOnlyList<PermissionRequestDto>>.Success(
                    new List<PermissionRequestDto>()));
            });

        // Act — fire Degraded first (sets _previousHealthState = Degraded), then Healthy
        FireHealthState(ConnectionHealthState.Degraded);
        FireHealthState(ConnectionHealthState.Healthy);

        await fetchTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — Degraded → Healthy is also a non-Healthy → Healthy transition
        await _apiClient.Received(1).GetPendingPermissionsAsync("sess-1", Arg.Any<CancellationToken>());
    }
}
