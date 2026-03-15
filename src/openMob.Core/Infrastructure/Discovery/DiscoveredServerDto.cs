namespace openMob.Core.Infrastructure.Discovery;

/// <summary>
/// Represents an opencode server instance discovered via mDNS on the local network.
/// </summary>
/// <param name="Name">The mDNS service instance name (e.g. "opencode-4096").</param>
/// <param name="Host">The resolved hostname or IP address.</param>
/// <param name="Port">The advertised port number (default 4096).</param>
/// <param name="DiscoveredAt">UTC timestamp of discovery.</param>
public sealed record DiscoveredServerDto(
    string Name,
    string Host,
    int Port,
    DateTimeOffset DiscoveredAt
);
