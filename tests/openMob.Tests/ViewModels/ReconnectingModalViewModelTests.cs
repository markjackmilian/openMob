using NSubstitute.ExceptionExtensions;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="ReconnectingModalViewModel"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ReconnectingModalViewModel.StartReconnectionLoopAsync"/> uses
/// <see cref="Task.Delay"/> with real delays (5 s, 10 s, 20 s). Tests that exercise
/// the loop pass a <see cref="CancellationToken"/> that cancels quickly (100–200 ms)
/// to avoid slow test runs. The loop exits gracefully on cancellation.
/// </para>
/// </remarks>
public sealed class ReconnectingModalViewModelTests
{
    private readonly IOpencodeConnectionManager _connectionManager;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;
    private readonly ReconnectingModalViewModel _sut;

    public ReconnectingModalViewModelTests()
    {
        _connectionManager = Substitute.For<IOpencodeConnectionManager>();
        _navigationService = Substitute.For<INavigationService>();
        _popupService = Substitute.For<IAppPopupService>();

        _sut = new ReconnectingModalViewModel(
            _connectionManager,
            _navigationService,
            _popupService);
    }

    // ─── Constructor / Initial State ──────────────────────────────────────────

    [Fact]
    public void Constructor_InitialState_TotalAttemptsIsThree()
    {
        // Assert
        _sut.TotalAttempts.Should().Be(3);
    }

    [Fact]
    public void Constructor_InitialState_IsReconnectingIsFalse()
    {
        // Assert
        _sut.IsReconnecting.Should().BeFalse();
    }

    [Fact]
    public void Constructor_InitialState_AttemptNumberIsZero()
    {
        // Assert
        _sut.AttemptNumber.Should().Be(0);
    }

    [Fact]
    public void Constructor_InitialState_StatusMessageIsNotEmpty()
    {
        // Assert
        _sut.StatusMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Constructor_WhenConnectionManagerIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ReconnectingModalViewModel(
            connectionManager: null!,
            _navigationService,
            _popupService);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WhenNavigationServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ReconnectingModalViewModel(
            _connectionManager,
            navigationService: null!,
            _popupService);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WhenPopupServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ReconnectingModalViewModel(
            _connectionManager,
            _navigationService,
            popupService: null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // ─── StartReconnectionLoopAsync — Success path ────────────────────────────

    [Fact]
    public async Task StartReconnectionLoopAsync_WhenServerReachableOnFirstAttempt_RaisesReconnectionSucceeded()
    {
        // Arrange
        _connectionManager
            .IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        var reconnectionSucceededFired = false;
        _sut.ReconnectionSucceeded += () => reconnectionSucceededFired = true;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        await _sut.StartReconnectionLoopAsync(cts.Token);

        // Assert
        reconnectionSucceededFired.Should().BeTrue();
    }

    [Fact]
    public async Task StartReconnectionLoopAsync_WhenServerReachableOnFirstAttempt_SetsAttemptNumberToOne()
    {
        // Arrange
        _connectionManager
            .IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        await _sut.StartReconnectionLoopAsync(cts.Token);

        // Assert
        _sut.AttemptNumber.Should().Be(1);
    }

    [Fact]
    public async Task StartReconnectionLoopAsync_WhenServerReachableOnFirstAttempt_SetsIsReconnectingFalseAfterProbe()
    {
        // Arrange
        _connectionManager
            .IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        await _sut.StartReconnectionLoopAsync(cts.Token);

        // Assert
        _sut.IsReconnecting.Should().BeFalse();
    }

    [Fact]
    public async Task StartReconnectionLoopAsync_WhenServerReachableOnFirstAttempt_CallsIsServerReachableAsyncOnce()
    {
        // Arrange
        _connectionManager
            .IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        await _sut.StartReconnectionLoopAsync(cts.Token);

        // Assert
        await _connectionManager.Received(1).IsServerReachableAsync(Arg.Any<CancellationToken>());
    }

    // ─── StartReconnectionLoopAsync — Failure / cancellation path ────────────

    [Fact]
    public async Task StartReconnectionLoopAsync_WhenCancelled_CompletesGracefully()
    {
        // Arrange
        _connectionManager
            .IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        var act = async () => await _sut.StartReconnectionLoopAsync(cts.Token);

        // Assert — OperationCanceledException must be swallowed; no exception propagates
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartReconnectionLoopAsync_WhenServerUnreachable_IncrementsAttemptNumber()
    {
        // Arrange
        _connectionManager
            .IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        // Cancel after the first probe completes but before the 5 s delay finishes.
        // The probe itself is synchronous (mock returns immediately), so 200 ms is enough
        // to let the first iteration set AttemptNumber = 1 before cancellation fires.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act
        await _sut.StartReconnectionLoopAsync(cts.Token);

        // Assert — at least one attempt was made
        _sut.AttemptNumber.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task StartReconnectionLoopAsync_WhenServerUnreachable_SetsStatusMessageContainingAttemptNumber()
    {
        // Arrange
        _connectionManager
            .IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act
        await _sut.StartReconnectionLoopAsync(cts.Token);

        // Assert — status message should contain the attempt number "1"
        _sut.StatusMessage.Should().Contain("1");
    }

    [Fact]
    public async Task StartReconnectionLoopAsync_WhenServerUnreachable_DoesNotRaiseReconnectionSucceeded()
    {
        // Arrange
        _connectionManager
            .IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        var reconnectionSucceededFired = false;
        _sut.ReconnectionSucceeded += () => reconnectionSucceededFired = true;

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act
        await _sut.StartReconnectionLoopAsync(cts.Token);

        // Assert
        reconnectionSucceededFired.Should().BeFalse();
    }

    [Fact]
    public async Task StartReconnectionLoopAsync_WhenServerUnreachable_SetsIsReconnectingTrueBeforeProbe()
    {
        // Arrange
        var isReconnectingDuringProbe = false;

        _connectionManager
            .IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                // Capture IsReconnecting state while the probe is in flight
                isReconnectingDuringProbe = _sut.IsReconnecting;
                await Task.Yield();
                return false;
            });

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act
        await _sut.StartReconnectionLoopAsync(cts.Token);

        // Assert
        isReconnectingDuringProbe.Should().BeTrue();
    }

    [Fact]
    public async Task StartReconnectionLoopAsync_WhenProbeThrowsOperationCancelled_CompletesGracefully()
    {
        // Arrange
        _connectionManager
            .IsServerReachableAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        var act = async () => await _sut.StartReconnectionLoopAsync(cts.Token);

        // Assert
        await act.Should().NotThrowAsync();
    }

    // ─── IsReconnecting during probe ──────────────────────────────────────────

    [Fact]
    public async Task IsReconnecting_DuringProbe_IsTrue()
    {
        // Arrange
        var isReconnectingObserved = false;

        _connectionManager
            .IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await Task.Delay(50);
                return true;
            });

        // Start the loop without awaiting so we can observe IsReconnecting mid-flight
        var loopTask = _sut.StartReconnectionLoopAsync(CancellationToken.None);

        // Act — give the loop a moment to start the probe and set IsReconnecting = true
        await Task.Delay(10);
        isReconnectingObserved = _sut.IsReconnecting;

        await loopTask;

        // Assert
        isReconnectingObserved.Should().BeTrue();
    }

    // ─── StatusMessage ────────────────────────────────────────────────────────

    [Fact]
    public async Task StatusMessage_OnFirstAttempt_ContainsAttemptNumber1()
    {
        // Arrange
        _connectionManager
            .IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act
        await _sut.StartReconnectionLoopAsync(cts.Token);

        // Assert
        _sut.StatusMessage.Should().Contain("1");
    }

    [Fact]
    public async Task StatusMessage_OnFirstAttempt_ContainsTotalAttempts()
    {
        // Arrange
        _connectionManager
            .IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        // Act
        await _sut.StartReconnectionLoopAsync(cts.Token);

        // Assert — TotalAttempts is 3; status message should contain "3"
        _sut.StatusMessage.Should().Contain("3");
    }

    // ─── NavigateToServerManagementCommand ───────────────────────────────────

    [Fact]
    public async Task NavigateToServerManagementCommand_WhenExecuted_CallsPopPopupAsync()
    {
        // Arrange
        _popupService.PopPopupAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _navigationService.GoToAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // Act
        await _sut.NavigateToServerManagementCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NavigateToServerManagementCommand_WhenExecuted_NavigatesToServerManagement()
    {
        // Arrange
        _popupService.PopPopupAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _navigationService.GoToAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // Act
        await _sut.NavigateToServerManagementCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.Received(1).GoToAsync(
            "///server-management",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NavigateToServerManagementCommand_WhenExecuted_CallsPopBeforeNavigate()
    {
        // Arrange
        var callOrder = new List<string>();

        _popupService
            .PopPopupAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callOrder.Add("pop");
                return Task.CompletedTask;
            });

        _navigationService
            .GoToAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callOrder.Add("navigate");
                return Task.CompletedTask;
            });

        // Act
        await _sut.NavigateToServerManagementCommand.ExecuteAsync(null);

        // Assert
        callOrder.Should().ContainInOrder("pop", "navigate");
    }

    [Fact]
    public async Task NavigateToServerManagementCommand_WhenExecuted_CallsPopExactlyOnce()
    {
        // Arrange
        _popupService.PopPopupAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _navigationService.GoToAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // Act
        await _sut.NavigateToServerManagementCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NavigateToServerManagementCommand_WhenExecuted_NavigatesExactlyOnce()
    {
        // Arrange
        _popupService.PopPopupAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _navigationService.GoToAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        // Act
        await _sut.NavigateToServerManagementCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.Received(1).GoToAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
