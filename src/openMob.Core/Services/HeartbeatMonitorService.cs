using openMob.Core.Models;

namespace openMob.Core.Services;

/// <summary>
/// Default implementation of <see cref="IHeartbeatMonitorService"/>.
/// Uses a <see cref="PeriodicTimer"/> with a 5-second interval to evaluate the connection
/// health state based on the elapsed time since the last recorded heartbeat.
/// </summary>
/// <remarks>
/// <para>
/// Thread safety: <c>_lastHeartbeatAtTicks</c> is written from the SSE background thread
/// (via <see cref="RecordHeartbeat"/>) and read from the <see cref="PeriodicTimer"/> callback.
/// <see cref="Interlocked.Exchange(ref long, long)"/> and <see cref="Interlocked.Read(ref long)"/>
/// are used to guarantee atomic access without locks.
/// </para>
/// <para>
/// The <see cref="TimeProvider"/> parameter is injectable for unit-test determinism.
/// Production code uses <see cref="TimeProvider.System"/> (the default).
/// </para>
/// </remarks>
internal sealed class HeartbeatMonitorService : IHeartbeatMonitorService
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// UTC ticks of the last recorded heartbeat.
    /// Written via <see cref="Interlocked.Exchange(ref long, long)"/>;
    /// read via <see cref="Interlocked.Read(ref long)"/>.
    /// </summary>
    private long _lastHeartbeatAtTicks;

    /// <summary>Current health state. Only mutated on the timer callback thread.</summary>
    private ConnectionHealthState _healthState = ConnectionHealthState.Healthy;

    /// <summary>CTS used to cancel the running periodic loop.</summary>
    private CancellationTokenSource? _loopCts;

    /// <summary>
    /// Initialises the service with an optional <see cref="TimeProvider"/>.
    /// </summary>
    /// <param name="timeProvider">
    /// The time provider to use for obtaining the current UTC time.
    /// Defaults to <see cref="TimeProvider.System"/> when <c>null</c>.
    /// </param>
    public HeartbeatMonitorService(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;

        // Assume healthy at startup — initialise the timestamp to now (via injected TimeProvider)
        // so the first periodic check does not immediately transition to Degraded or Lost.
        _lastHeartbeatAtTicks = _timeProvider.GetUtcNow().UtcTicks;
    }

    /// <inheritdoc />
    public ConnectionHealthState HealthState => _healthState;

    /// <inheritdoc />
    public event Action<ConnectionHealthState>? HealthStateChanged;

    /// <inheritdoc />
    public void RecordHeartbeat()
    {
        var nowTicks = _timeProvider.GetUtcNow().UtcTicks;
        Interlocked.Exchange(ref _lastHeartbeatAtTicks, nowTicks);

        if (_healthState == ConnectionHealthState.Healthy)
            return;

        _healthState = ConnectionHealthState.Healthy;
        HealthStateChanged?.Invoke(ConnectionHealthState.Healthy);
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken ct)
    {
        // Cancel any previously running loop before starting a new one.
        if (_loopCts is not null)
        {
            await _loopCts.CancelAsync().ConfigureAwait(false);
            _loopCts.Dispose();
        }

        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var loopToken = _loopCts.Token;

        _ = Task.Run(async () => await RunLoopAsync(loopToken).ConfigureAwait(false), loopToken);
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        if (_loopCts is not null)
        {
            _loopCts.Cancel();
            _loopCts.Dispose();
            _loopCts = null;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// The internal periodic check loop. Runs every 5 seconds and evaluates the elapsed time
    /// since the last heartbeat to determine the new health state.
    /// </summary>
    /// <param name="ct">Cancellation token that stops the loop.</param>
    private async Task RunLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                var nowTicks = _timeProvider.GetUtcNow().UtcTicks;
                var lastTicks = Interlocked.Read(ref _lastHeartbeatAtTicks);
                var elapsedSeconds = (nowTicks - lastTicks) / TimeSpan.TicksPerSecond;

                var newState = elapsedSeconds switch
                {
                    <= 30 => ConnectionHealthState.Healthy,
                    <= 60 => ConnectionHealthState.Degraded,
                    _ => ConnectionHealthState.Lost,
                };

                if (newState == _healthState)
                    continue;

                _healthState = newState;
                HealthStateChanged?.Invoke(newState);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when StopAsync() or the external CancellationToken fires — exit silently.
        }
    }
}
