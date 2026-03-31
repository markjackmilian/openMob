namespace openMob.Core.Models;

/// <summary>Represents the traffic-light health state of the server connection, driven by heartbeat events.</summary>
public enum ConnectionHealthState
{
    /// <summary>Last heartbeat received within 30 seconds. Connection is healthy.</summary>
    Healthy,

    /// <summary>No heartbeat received for 30–60 seconds. Connection may be degraded.</summary>
    Degraded,

    /// <summary>No heartbeat received for more than 60 seconds. Connection is considered lost.</summary>
    Lost,
}
