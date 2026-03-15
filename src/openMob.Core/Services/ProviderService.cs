using System.Text.Json;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;
using openMob.Core.Infrastructure.Monitoring;

namespace openMob.Core.Services;

/// <summary>
/// Implementation of <see cref="IProviderService"/> that wraps <see cref="IOpencodeApiClient"/>
/// provider methods with error handling and result unwrapping.
/// </summary>
internal sealed class ProviderService : IProviderService
{
    private readonly IOpencodeApiClient _apiClient;

    /// <summary>Initialises the service with the opencode API client.</summary>
    /// <param name="apiClient">The opencode API client.</param>
    public ProviderService(IOpencodeApiClient apiClient)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        _apiClient = apiClient;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProviderDto>> GetProvidersAsync(CancellationToken ct = default)
    {
        var result = await _apiClient.GetProvidersAsync(ct).ConfigureAwait(false);

        if (result.IsSuccess && result.Value is not null)
            return result.Value;

        if (result.Error is not null)
        {
            SentryHelper.CaptureException(
                new InvalidOperationException($"Failed to get providers: {result.Error.Message}"),
                new Dictionary<string, object> { ["errorKind"] = result.Error.Kind.ToString() });
        }

        return [];
    }

    /// <inheritdoc />
    public async Task<bool> SetProviderAuthAsync(string providerId, string apiKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        // Build the auth JSON object with the API key
        var authJson = JsonSerializer.SerializeToElement(new { key = apiKey });
        var request = new SetProviderAuthRequest(Auth: authJson);

        var result = await _apiClient.SetProviderAuthAsync(providerId, request, ct).ConfigureAwait(false);

        if (result.IsSuccess)
            return true;

        if (result.Error is not null)
        {
            SentryHelper.CaptureException(
                new InvalidOperationException($"Failed to set provider auth for '{providerId}': {result.Error.Message}"),
                new Dictionary<string, object>
                {
                    ["providerId"] = providerId,
                    ["errorKind"] = result.Error.Kind.ToString(),
                });
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<bool> HasAnyProviderConfiguredAsync(CancellationToken ct = default)
    {
        var providers = await GetProvidersAsync(ct).ConfigureAwait(false);
        return providers.Any(p => !string.IsNullOrWhiteSpace(p.Key));
    }
}
