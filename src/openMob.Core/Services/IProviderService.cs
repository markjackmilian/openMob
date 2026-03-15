using openMob.Core.Infrastructure.Http.Dtos.Opencode;

namespace openMob.Core.Services;

/// <summary>
/// Service interface for AI provider and model operations.
/// Wraps <see cref="Infrastructure.Http.IOpencodeApiClient"/> provider methods.
/// </summary>
public interface IProviderService
{
    /// <summary>Gets all configured providers from the opencode server.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of all providers, or an empty list on failure.</returns>
    Task<IReadOnlyList<ProviderDto>> GetProvidersAsync(CancellationToken ct = default);

    /// <summary>Sets the API key authentication for a provider.</summary>
    /// <param name="providerId">The provider identifier.</param>
    /// <param name="apiKey">The API key to set.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the auth was set successfully; <c>false</c> otherwise.</returns>
    Task<bool> SetProviderAuthAsync(string providerId, string apiKey, CancellationToken ct = default);

    /// <summary>Checks whether any provider has a configured API key.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if at least one provider has a key configured; <c>false</c> otherwise.</returns>
    Task<bool> HasAnyProviderConfiguredAsync(CancellationToken ct = default);
}
