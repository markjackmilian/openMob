using openMob.Core.Models;
using openMob.Core.Services;

namespace openMob.Tests.Services;

/// <summary>
/// Unit tests for <see cref="HeartbeatMonitorService"/>.
/// </summary>
/// <remarks>
/// <para>
/// Time-based state transitions (Degraded at 30 s, Lost at 60 s) are driven by
/// <see cref="PeriodicTimer"/> which is not directly fakeable. Those transitions are
/// tested by injecting a <see cref="FakeTimeProvider"/> that controls
/// <see cref="TimeProvider.GetUtcNow"/> so the elapsed-ticks calculation inside
/// <see cref="HeartbeatMonitorService.RunLoopAsync"/> sees the desired elapsed time.
/// The timer itself still fires on real time, but with a very short period (50 ms)
/// so tests complete quickly.
/// </para>
/// <para>
/// Tests that require the timer loop to tick use <see cref="Task.Delay"/> with a
/// generous but bounded timeout (500 ms) to wait for the state transition, then
/// assert the result.
/// </para>
/// </remarks>
public sealed class HeartbeatMonitorServiceTests
{
    // ─── Fake TimeProvider ────────────────────────────────────────────────────

    /// <summary>
    /// A <see cref="TimeProvider"/> whose current UTC time can be advanced manually.
    /// Used to control the elapsed-ticks calculation inside <see cref="HeartbeatMonitorService"/>
    /// without waiting for real wall-clock time.
    /// </summary>
    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset? startTime = null)
        {
            _utcNow = startTime ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        /// <summary>Advances the fake clock by the specified amount.</summary>
        public void Advance(TimeSpan amount) => _utcNow = _utcNow.Add(amount);
    }

    // ─── Constructor / Initial State ──────────────────────────────────────────

    [Fact]
    public void Constructor_InitialState_IsHealthy()
    {
        // Arrange + Act
        var sut = new HeartbeatMonitorService();

        // Assert
        sut.HealthState.Should().Be(ConnectionHealthState.Healthy);
    }

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_UsesSystemTimeProvider()
    {
        // Arrange + Act
        var sut = new HeartbeatMonitorService(timeProvider: null);

        // Assert — no exception thrown; initial state is Healthy
        sut.HealthState.Should().Be(ConnectionHealthState.Healthy);
    }

    // ─── RecordHeartbeat ──────────────────────────────────────────────────────

    [Fact]
    public void RecordHeartbeat_WhenAlreadyHealthy_DoesNotRaiseHealthStateChanged()
    {
        // Arrange
        var sut = new HeartbeatMonitorService();
        var eventFiredCount = 0;
        sut.HealthStateChanged += _ => eventFiredCount++;

        // Act
        sut.RecordHeartbeat();

        // Assert
        eventFiredCount.Should().Be(0);
    }

    [Fact]
    public void RecordHeartbeat_WhenAlreadyHealthy_StateRemainsHealthy()
    {
        // Arrange
        var sut = new HeartbeatMonitorService();

        // Act
        sut.RecordHeartbeat();

        // Assert
        sut.HealthState.Should().Be(ConnectionHealthState.Healthy);
    }

    [Fact]
    public void RecordHeartbeat_WhenCalledMultipleTimes_DoesNotRaiseHealthStateChanged()
    {
        // Arrange
        var sut = new HeartbeatMonitorService();
        var eventFiredCount = 0;
        sut.HealthStateChanged += _ => eventFiredCount++;

        // Act
        sut.RecordHeartbeat();
        sut.RecordHeartbeat();
        sut.RecordHeartbeat();

        // Assert
        eventFiredCount.Should().Be(0);
    }

    [Fact]
    public async Task RecordHeartbeat_WhenDegraded_TransitionsToHealthy()
    {
        // Arrange — drive the service into Degraded state via the public API
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var sut = new HeartbeatMonitorService(fakeTime);

        // Advance the fake clock by 35 s so the timer loop sees elapsed > 30 s → Degraded
        fakeTime.Advance(TimeSpan.FromSeconds(35));

        await sut.StartAsync(CancellationToken.None);

        var deadline = DateTime.UtcNow.AddSeconds(6);
        while (sut.HealthState != ConnectionHealthState.Degraded && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        await sut.StopAsync();

        // Act
        sut.RecordHeartbeat();

        // Assert
        sut.HealthState.Should().Be(ConnectionHealthState.Healthy);
    }

    [Fact]
    public async Task RecordHeartbeat_WhenDegraded_RaisesHealthStateChangedWithHealthy()
    {
        // Arrange — drive the service into Degraded state via the public API
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var sut = new HeartbeatMonitorService(fakeTime);

        fakeTime.Advance(TimeSpan.FromSeconds(35));

        await sut.StartAsync(CancellationToken.None);

        var deadline = DateTime.UtcNow.AddSeconds(6);
        while (sut.HealthState != ConnectionHealthState.Degraded && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        await sut.StopAsync();

        // Subscribe after reaching Degraded so only the Healthy transition is captured
        ConnectionHealthState? receivedState = null;
        sut.HealthStateChanged += s => receivedState = s;

        // Act
        sut.RecordHeartbeat();

        // Assert
        receivedState.Should().Be(ConnectionHealthState.Healthy);
    }

    [Fact]
    public async Task RecordHeartbeat_WhenLost_TransitionsToHealthy()
    {
        // Arrange — drive the service into Lost state via the public API
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var sut = new HeartbeatMonitorService(fakeTime);

        // Advance the fake clock by 65 s so the timer loop sees elapsed > 60 s → Lost
        fakeTime.Advance(TimeSpan.FromSeconds(65));

        await sut.StartAsync(CancellationToken.None);

        var deadline = DateTime.UtcNow.AddSeconds(6);
        while (sut.HealthState != ConnectionHealthState.Lost && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        await sut.StopAsync();

        // Act
        sut.RecordHeartbeat();

        // Assert
        sut.HealthState.Should().Be(ConnectionHealthState.Healthy);
    }

    [Fact]
    public async Task RecordHeartbeat_WhenLost_RaisesHealthStateChangedWithHealthy()
    {
        // Arrange — drive the service into Lost state via the public API
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var sut = new HeartbeatMonitorService(fakeTime);

        fakeTime.Advance(TimeSpan.FromSeconds(65));

        await sut.StartAsync(CancellationToken.None);

        var deadline = DateTime.UtcNow.AddSeconds(6);
        while (sut.HealthState != ConnectionHealthState.Lost && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        await sut.StopAsync();

        // Subscribe after reaching Lost so only the Healthy transition is captured
        ConnectionHealthState? receivedState = null;
        sut.HealthStateChanged += s => receivedState = s;

        // Act
        sut.RecordHeartbeat();

        // Assert
        receivedState.Should().Be(ConnectionHealthState.Healthy);
    }

    [Fact]
    public async Task RecordHeartbeat_WhenDegraded_RaisesHealthStateChangedExactlyOnce()
    {
        // Arrange — drive the service into Degraded state via the public API
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var sut = new HeartbeatMonitorService(fakeTime);

        fakeTime.Advance(TimeSpan.FromSeconds(35));

        await sut.StartAsync(CancellationToken.None);

        var deadline = DateTime.UtcNow.AddSeconds(6);
        while (sut.HealthState != ConnectionHealthState.Degraded && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        await sut.StopAsync();

        // Subscribe after reaching Degraded so only the Healthy transition is counted
        var eventFiredCount = 0;
        sut.HealthStateChanged += _ => eventFiredCount++;

        // Act
        sut.RecordHeartbeat();

        // Assert
        eventFiredCount.Should().Be(1);
    }

    // ─── Timer-driven state transitions ───────────────────────────────────────

    [Fact]
    public async Task StartAsync_WhenElapsedExceeds30Seconds_TransitionsToDegraded()
    {
        // Arrange
        // Start the fake clock at the real current time so that the initial
        // _lastHeartbeatAtTicks (set from TimeProvider.System in the constructor)
        // is close to the fake's start time. Advancing by 35 s then produces
        // a positive elapsed time that exceeds the 30 s Degraded threshold.
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var sut = new HeartbeatMonitorService(fakeTime);

        // Advance the fake clock by 35 seconds so the next timer tick sees elapsed > 30 s
        fakeTime.Advance(TimeSpan.FromSeconds(35));

        ConnectionHealthState? observedState = null;
        sut.HealthStateChanged += s => observedState = s;

        // Act — start the loop and poll until the state changes or 6 s elapses
        await sut.StartAsync(CancellationToken.None);

        var deadline = DateTime.UtcNow.AddSeconds(6);
        while (observedState is null && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        await sut.StopAsync();

        // Assert
        observedState.Should().Be(ConnectionHealthState.Degraded);
    }

    [Fact]
    public async Task StartAsync_WhenElapsedExceeds60Seconds_TransitionsToLost()
    {
        // Arrange
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var sut = new HeartbeatMonitorService(fakeTime);

        // Advance the fake clock by 65 seconds so the next timer tick sees elapsed > 60 s
        fakeTime.Advance(TimeSpan.FromSeconds(65));

        ConnectionHealthState? observedState = null;
        sut.HealthStateChanged += s => observedState = s;

        // Act
        await sut.StartAsync(CancellationToken.None);

        var deadline = DateTime.UtcNow.AddSeconds(6);
        while (observedState is null && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        await sut.StopAsync();

        // Assert
        observedState.Should().Be(ConnectionHealthState.Lost);
    }

    [Fact]
    public async Task StartAsync_WhenElapsedIsWithin30Seconds_DoesNotRaiseHealthStateChanged()
    {
        // Arrange
        // Start at real current time and do NOT advance — elapsed is ~0 s,
        // well within the 30 s Healthy threshold.
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var sut = new HeartbeatMonitorService(fakeTime);

        var eventFiredCount = 0;
        sut.HealthStateChanged += _ => eventFiredCount++;

        // Act — start the loop and wait for at least one tick (5 s + buffer)
        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(6));
        await sut.StopAsync();

        // Assert — state was already Healthy; no transition should have fired
        eventFiredCount.Should().Be(0);
    }

    [Fact]
    public async Task StartAsync_WhenCalledTwice_ReplacesExistingLoop()
    {
        // Arrange
        var sut = new HeartbeatMonitorService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act — calling StartAsync twice must not throw
        await sut.StartAsync(cts.Token);
        var act = async () => await sut.StartAsync(cts.Token);

        // Assert
        await act.Should().NotThrowAsync();

        await sut.StopAsync();
    }

    // ─── StopAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task StopAsync_WhenLoopIsRunning_CompletesWithoutException()
    {
        // Arrange
        var sut = new HeartbeatMonitorService();
        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);

        // Act
        var act = async () => await sut.StopAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_WhenLoopIsNotRunning_CompletesWithoutException()
    {
        // Arrange
        var sut = new HeartbeatMonitorService();

        // Act — stop without ever starting
        var act = async () => await sut.StopAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_WhenCalledTwice_CompletesWithoutException()
    {
        // Arrange
        var sut = new HeartbeatMonitorService();
        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await sut.StopAsync();

        // Act
        var act = async () => await sut.StopAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HealthState_AfterStop_RemainsAtLastKnownState()
    {
        // Arrange
        var sut = new HeartbeatMonitorService();
        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);

        // Act
        await sut.StopAsync();

        // Assert — state should still be Healthy (no time has elapsed)
        sut.HealthState.Should().Be(ConnectionHealthState.Healthy);
    }

    // ─── ExternalCancellationToken ────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_WhenExternalTokenCancelled_LoopExitsGracefully()
    {
        // Arrange
        var sut = new HeartbeatMonitorService();
        using var cts = new CancellationTokenSource();

        // Act
        await sut.StartAsync(cts.Token);
        cts.Cancel();

        // Give the loop a moment to observe the cancellation
        await Task.Delay(100);

        // Assert — no exception propagated; HealthState is still accessible
        var act = () => sut.HealthState;
        act.Should().NotThrow();
    }

    // ─── HealthStateChanged event ─────────────────────────────────────────────

    [Fact]
    public async Task HealthStateChanged_WhenNoSubscribers_RecordHeartbeatDoesNotThrow()
    {
        // Arrange — drive the service into Degraded state via the public API
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var sut = new HeartbeatMonitorService(fakeTime);

        fakeTime.Advance(TimeSpan.FromSeconds(35));

        await sut.StartAsync(CancellationToken.None);

        var deadline = DateTime.UtcNow.AddSeconds(6);
        while (sut.HealthState != ConnectionHealthState.Degraded && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        await sut.StopAsync();

        // Act — no subscribers attached; should not throw NullReferenceException
        var act = () => sut.RecordHeartbeat();

        // Assert
        act.Should().NotThrow();
    }
}
