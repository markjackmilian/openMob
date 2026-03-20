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

    /// <summary>
    /// Gets only primary-mode agents that are visible to the user.
    /// Returns agents where <c>Mode == "primary"</c> or <c>Mode == "all"</c>,
    /// excluding agents with <c>Hidden == true</c> (system agents like compaction, title, summary).
    /// The unfiltered list is available via <see cref="GetAgentsAsync"/>.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of primary agents, or an empty list on failure.</returns>
    Task<IReadOnlyList<AgentDto>> GetPrimaryAgentsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets only subagent-mode agents.
    /// Returns agents where <c>Mode == "subagent"</c> or <c>Mode == "all"</c>.
    /// Hidden agents are NOT excluded (subagents may be hidden from primary picker but still invocable).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of subagent agents, or an empty list on failure.</returns>
    Task<IReadOnlyList<AgentDto>> GetSubagentAgentsAsync(CancellationToken ct = default);
}
