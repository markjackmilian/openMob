using openMob.Core.Infrastructure.Dtos;

namespace openMob.Core.Infrastructure.Discovery;

/// <summary>
/// Service for discovering opencode server instances on the local network via mDNS.
/// </summary>
/// <remarks>
/// <para>
/// opencode advertises itself using the <c>_http._tcp.local.</c> mDNS service type
/// (via the <c>bonjour-service</c> Node.js library) with service names matching the
/// pattern <c>opencode-{port}</c> (e.g. <c>opencode-4096</c>).
/// </para>
/// <para><strong>Platform requirements:</strong></para>
/// <list type="bullet">
///   <item>
///     <description>
///       <strong>Android:</strong> The <c>CHANGE_WIFI_MULTICAST_STATE</c> permission must be
///       declared in <c>AndroidManifest.xml</c>. The caller is responsible for acquiring and
///       releasing a <c>WifiManager.MulticastLock</c> before and after calling <see cref="ScanAsync"/>.
///     </description>
///   </item>
///   <item>
///     <description>
///       <strong>iOS:</strong> <c>NSLocalNetworkUsageDescription</c> must be set in <c>Info.plist</c>
///       and <c>_http._tcp</c> must be listed in <c>NSBonjourServices</c>. On iOS 14.5+, Zeroconf
///       automatically uses the <c>NSNetServiceBrowser</c> workaround.
///     </description>
///   </item>
/// </list>
/// </remarks>
public interface IOpencodeDiscoveryService
{
    /// <summary>
    /// Starts a mDNS scan and yields discovered opencode servers as they are found.
    /// Completes naturally after a 10-second timeout or when <paramref name="cancellationToken"/> is cancelled.
    /// Never throws — yields zero results if the network is unavailable or mDNS is blocked.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the scan early.</param>
    /// <returns>An async sequence of <see cref="DiscoveredServerDto"/> instances.</returns>
    IAsyncEnumerable<DiscoveredServerDto> ScanAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a health check against the discovered server by calling <c>GET /global/health</c>.
    /// Uses a 5-second timeout. Never throws.
    /// </summary>
    /// <param name="server">The discovered server to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the server responds with a healthy status; <c>false</c> otherwise.</returns>
    Task<bool> ValidateServerAsync(DiscoveredServerDto server, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the server and, if healthy, persists it as a <see cref="ServerConnectionDto"/>
    /// with <c>DiscoveredViaMdns = true</c>.
    /// </summary>
    /// <param name="server">The discovered server to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The created <see cref="ServerConnectionDto"/> on success;
    /// <c>null</c> if validation fails (server unreachable or unhealthy).
    /// </returns>
    Task<ServerConnectionDto?> SaveDiscoveredServerAsync(DiscoveredServerDto server, CancellationToken cancellationToken = default);
}
