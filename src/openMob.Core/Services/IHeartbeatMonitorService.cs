using openMob.Core.Models;

namespace openMob.Core.Services;

/// <summary>
/// Monitors the server heartbeat and exposes the current connection health state.
/// </summary>
/// <remarks>
/// <para>
/// The service records heartbeat timestamps via <see cref="RecordHeartbeat"/> (called from the SSE
/// background thread) and runs a periodic 5-second check loop (started via <see cref="StartAsync"/>)
/// that transitions the health state through the traffic-light model:
/// <list type="bullet">
///   <item><description><see cref="ConnectionHealthState.Healthy"/> — last heartbeat within 30 s</description></item>
///   <item><description><see cref="ConnectionHealthState.Degraded"/> — no heartbeat for 30–60 s</description></item>
///   <item><description><see cref="ConnectionHealthState.Lost"/> — no heartbeat for more than 60 s</description></item>
/// </list>
/// </para>
/// <para>
/// <see cref="HealthStateChanged"/> is raised only on actual state transitions, never on repeated
/// checks that produce the same state.
/// </para>
/// </remarks>
public interface IHeartbeatMonitorService
{
    /// <summary>Gets the current connection health state.</summary>
    ConnectionHealthState HealthState { get; }

    /// <summary>
    /// Raised whenever <see cref="HealthState"/> transitions to a new value.
    /// Subscribers receive the new state.
    /// </summary>
    event Action<ConnectionHealthState>? HealthStateChanged;

    /// <summary>
    /// Records that a heartbeat was received right now.
    /// Immediately resets <see cref="HealthState"/> to <see cref="ConnectionHealthState.Healthy"/>
    /// and raises <see cref="HealthStateChanged"/> if the state was not already <c>Healthy</c>.
    /// </summary>
    /// <remarks>
    /// This method is thread-safe and may be called from any thread (e.g. the SSE background thread).
    /// </remarks>
    void RecordHeartbeat();

    /// <summary>
    /// Starts the periodic 5-second health-check loop.
    /// If a loop is already running, it is cancelled and replaced with a new one.
    /// </summary>
    /// <param name="ct">
    /// Cancellation token. When cancelled, the loop exits gracefully without raising an exception.
    /// </param>
    Task StartAsync(CancellationToken ct);

    /// <summary>
    /// Stops the periodic health-check loop.
    /// </summary>
    /// <returns>A completed task.</returns>
    Task StopAsync();
}
