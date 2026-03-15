using openMob.Core.Infrastructure.Http.Dtos.Opencode;

namespace openMob.Core.Services;

/// <summary>
/// Service interface for agent operations.
/// Wraps <see cref="Infrastructure.Http.IOpencodeApiClient"/> agent methods.
/// </summary>
public interface IAgentService
{
    /// <summary>Gets all available agents from the opencode server.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of all agents, or an empty list on failure.</returns>
    Task<IReadOnlyList<AgentDto>> GetAgentsAsync(CancellationToken ct = default);
}
