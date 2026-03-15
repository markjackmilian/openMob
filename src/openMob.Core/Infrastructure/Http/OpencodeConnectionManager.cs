using System.Net.Http;
using System.Text;
using openMob.Core.Data.Repositories;
using openMob.Core.Infrastructure.Security;

namespace openMob.Core.Infrastructure.Http;

/// <summary>
/// Default implementation of <see cref="IOpencodeConnectionManager"/>.
/// Reads the active <c>ServerConnection</c> from <see cref="IServerConnectionRepository"/>
/// and retrieves credentials from <see cref="IServerCredentialStore"/>.
/// </summary>
internal sealed class OpencodeConnectionManager : IOpencodeConnectionManager
{
    private readonly IServerConnectionRepository _repository;
    private readonly IServerCredentialStore _credentialStore;
    private readonly IHttpClientFactory _httpClientFactory;
    private volatile ServerConnectionStatus _connectionStatus = ServerConnectionStatus.Disconnected;

    /// <summary>
    /// Initialises the connection manager with the required dependencies.
    /// </summary>
    /// <param name="repository">Repository for reading the active server connection.</param>
    /// <param name="credentialStore">Secure store for reading server passwords.</param>
    /// <param name="httpClientFactory">Factory for creating HTTP clients used in health checks.</param>
    public OpencodeConnectionManager(
        IServerConnectionRepository repository,
        IServerCredentialStore credentialStore,
        IHttpClientFactory httpClientFactory)
    {
        _repository = repository;
        _credentialStore = credentialStore;
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public ServerConnectionStatus ConnectionStatus
    {
        get => _connectionStatus;
        private set
        {
            if (_connectionStatus == value)
                return;

            _connectionStatus = value;
            StatusChanged?.Invoke(value);
        }
    }

    /// <inheritdoc />
    public event Action<ServerConnectionStatus>? StatusChanged;

    /// <inheritdoc />
    public async Task<string?> GetBaseUrlAsync(CancellationToken ct = default)
    {
        var dto = await _repository.GetActiveAsync(ct).ConfigureAwait(false);
        if (dto is null)
            return null;

        return $"http://{dto.Host}:{dto.Port}";
    }

    /// <inheritdoc />
    public async Task<string?> GetBasicAuthHeaderAsync(CancellationToken ct = default)
    {
        var dto = await _repository.GetActiveAsync(ct).ConfigureAwait(false);
        if (dto is null || dto.Username is null)
            return null;

        var password = await _credentialStore.GetPasswordAsync(dto.Id, ct).ConfigureAwait(false);
        if (password is null)
            return null;

        var credentials = $"{dto.Username}:{password}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
        return $"Basic {encoded}";
    }

    /// <inheritdoc />
    public async Task<bool> IsServerReachableAsync(CancellationToken ct = default)
    {
        var baseUrl = await GetBaseUrlAsync(ct).ConfigureAwait(false);
        if (baseUrl is null)
            return false;

        try
        {
            var client = _httpClientFactory.CreateClient("opencode");
            using var response = await client.GetAsync($"{baseUrl}/global/health", ct).ConfigureAwait(false);
            var reachable = response.IsSuccessStatusCode;
            ConnectionStatus = reachable ? ServerConnectionStatus.Connected : ServerConnectionStatus.Error;
            return reachable;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            SetConnectionStatus(ServerConnectionStatus.Error);
            return false;
        }
    }

    /// <inheritdoc />
    public void SetConnectionStatus(ServerConnectionStatus status) => ConnectionStatus = status;
}
