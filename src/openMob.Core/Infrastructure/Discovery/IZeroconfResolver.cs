using Zeroconf;

namespace openMob.Core.Infrastructure.Discovery;

/// <summary>
/// Abstraction over the Zeroconf mDNS resolver, enabling unit testing without real network calls.
/// </summary>
/// <remarks>
/// This interface is public to satisfy C# accessibility rules — <see cref="OpencodeDiscoveryService"/>
/// is a public class and its constructor parameter types must also be at least as accessible.
/// Consumers should not depend on this interface directly; use <see cref="IOpencodeDiscoveryService"/> instead.
/// </remarks>
public interface IZeroconfResolver
{
    /// <summary>
    /// Resolves mDNS services of the given protocol type on the local network.
    /// </summary>
    /// <param name="protocol">The mDNS service type (e.g. "_http._tcp.local.").</param>
    /// <param name="scanTime">Maximum duration to scan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of discovered <see cref="IZeroconfHost"/> instances.</returns>
    Task<IReadOnlyList<IZeroconfHost>> ResolveAsync(
        string protocol,
        TimeSpan scanTime,
        CancellationToken cancellationToken);
}
