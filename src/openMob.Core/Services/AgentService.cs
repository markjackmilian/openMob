using System.Linq;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Monitoring;

namespace openMob.Core.Services;

/// <summary>
/// Implementation of <see cref="IAgentService"/> that wraps <see cref="IOpencodeApiClient"/>
/// agent methods with error handling and result unwrapping.
/// </summary>
internal sealed class AgentService : IAgentService
{
    private readonly IOpencodeApiClient _apiClient;

    /// <summary>Initialises the service with the opencode API client.</summary>
    /// <param name="apiClient">The opencode API client.</param>
    public AgentService(IOpencodeApiClient apiClient)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        _apiClient = apiClient;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentDto>> GetAgentsAsync(CancellationToken ct = default)
    {
        var result = await _apiClient.GetAgentsAsync(ct).ConfigureAwait(false);

        if (result.IsSuccess && result.Value is not null)
            return result.Value;

        if (result.Error is not null)
        {
            SentryHelper.CaptureException(
                new InvalidOperationException($"Failed to get agents: {result.Error.Message}"),
                new Dictionary<string, object> { ["errorKind"] = result.Error.Kind.ToString() });
        }

        return [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentDto>> GetPrimaryAgentsAsync(CancellationToken ct = default)
    {
        var all = await GetAgentsAsync(ct).ConfigureAwait(false);
        return all.Where(a => (a.Mode is "primary" or "all") && !a.Hidden).ToList();
    }
}
