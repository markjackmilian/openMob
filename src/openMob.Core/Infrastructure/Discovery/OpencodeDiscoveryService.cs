using System.Runtime.CompilerServices;
using System.Text.Json;
using openMob.Core.Data.Repositories;
using openMob.Core.Infrastructure.Dtos;
using Zeroconf;

namespace openMob.Core.Infrastructure.Discovery;

/// <summary>
/// Discovers opencode server instances on the local network using mDNS via the Zeroconf library.
/// </summary>
/// <remarks>
/// <para>
/// opencode advertises using <c>_http._tcp.local.</c> with service names matching <c>opencode-{port}</c>.
/// This service queries that type and filters by name prefix to avoid false positives from other
/// HTTP services on the network.
/// </para>
/// <para>
/// <strong>Android note:</strong> The caller (MAUI layer) is responsible for acquiring and releasing
/// a <c>WifiManager.MulticastLock</c> before and after calling <see cref="ScanAsync"/>. The
/// <c>CHANGE_WIFI_MULTICAST_STATE</c> permission must also be declared in <c>AndroidManifest.xml</c>.
/// </para>
/// <para>
/// <strong>iOS note:</strong> <c>NSLocalNetworkUsageDescription</c> and <c>NSBonjourServices</c>
/// (containing <c>_http._tcp</c>) must be configured in <c>Info.plist</c>. On iOS 14.5+, Zeroconf
/// automatically uses the <c>NSNetServiceBrowser</c> workaround — no additional code is required.
/// </para>
/// </remarks>
public sealed class OpencodeDiscoveryService : IOpencodeDiscoveryService
{
    private const int ScanTimeoutSeconds = 10;
    private const string MdnsServiceType = "_http._tcp.local.";
    private const string ServiceNamePrefix = "opencode-";
    private const string HealthEndpointPath = "/global/health";

    private readonly IZeroconfResolver _resolver;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServerConnectionRepository _repository;

    /// <summary>
    /// Initialises a new instance of <see cref="OpencodeDiscoveryService"/>.
    /// </summary>
    /// <param name="resolver">The mDNS resolver abstraction (injected for testability).</param>
    /// <param name="httpClientFactory">Factory used to create short-lived HTTP clients for health checks.</param>
    /// <param name="repository">Repository used to persist validated discovered servers.</param>
    public OpencodeDiscoveryService(
        IZeroconfResolver resolver,
        IHttpClientFactory httpClientFactory,
        IServerConnectionRepository repository)
    {
        _resolver = resolver;
        _httpClientFactory = httpClientFactory;
        _repository = repository;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<DiscoveredServerDto> ScanAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IZeroconfHost> hosts;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(ScanTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            hosts = await _resolver.ResolveAsync(
                MdnsServiceType,
                TimeSpan.FromSeconds(ScanTimeoutSeconds),
                linkedCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Scan was cancelled by the caller or timed out — complete gracefully with zero results.
            yield break;
        }
        catch (Exception)
        {
            // Network unavailable, mDNS blocked, or any other resolver error — yield nothing (REQ-009).
            yield break;
        }

        var seen = new HashSet<(string Host, int Port)>();

        foreach (var host in hosts)
        {
            if (!host.DisplayName.StartsWith(ServiceNamePrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            // Default to 4096 — the standard opencode server port — if no service record is present
            var port = host.Services.Values.FirstOrDefault()?.Port ?? 4096;
            var key = (host.IPAddress, port);

            if (!seen.Add(key))
                continue;

            yield return new DiscoveredServerDto(
                Name: host.DisplayName,
                Host: host.IPAddress,
                Port: port,
                DiscoveredAt: DateTimeOffset.UtcNow);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateServerAsync(
        DiscoveredServerDto server,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use the named "discovery-probe" client — timeout is pre-configured at registration.
            // Do NOT dispose factory-managed HttpClient instances (no `using`).
            var client = _httpClientFactory.CreateClient("discovery-probe");

            var url = $"http://{server.Host}:{server.Port}{HealthEndpointPath}";
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return false;

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("healthy", out var healthyProp))
                return healthyProp.ValueKind == JsonValueKind.True;

            return false;
        }
        catch (Exception)
        {
            // HttpRequestException, TaskCanceledException, JsonException, or any other failure.
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<ServerConnectionDto?> SaveDiscoveredServerAsync(
        DiscoveredServerDto server,
        CancellationToken cancellationToken = default)
    {
        var isValid = await ValidateServerAsync(server, cancellationToken).ConfigureAwait(false);

        if (!isValid)
            return null;

        var dto = new ServerConnectionDto(
            Id: string.Empty,
            Name: server.Name,
            Host: server.Host,
            Port: server.Port,
            Username: null,
            IsActive: false,
            DiscoveredViaMdns: true,
            UseHttps: false,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            HasPassword: false);

        return await _repository.AddAsync(dto, cancellationToken).ConfigureAwait(false);
    }
}
