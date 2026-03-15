using Zeroconf;

namespace openMob.Core.Infrastructure.Discovery;

/// <summary>
/// Production adapter that delegates to <see cref="ZeroconfResolver.ResolveAsync"/>.
/// </summary>
/// <remarks>
/// This thin wrapper exists solely to allow <see cref="OpencodeDiscoveryService"/> to be unit-tested
/// without performing real mDNS network calls. In production, this adapter is the only implementation
/// of <see cref="IZeroconfResolver"/> and is registered as a singleton in DI.
/// </remarks>
public sealed class ZeroconfResolverAdapter : IZeroconfResolver
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<IZeroconfHost>> ResolveAsync(
        string protocol,
        TimeSpan scanTime,
        CancellationToken cancellationToken)
    {
        return await ZeroconfResolver.ResolveAsync(
            protocol,
            scanTime: scanTime,
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}
