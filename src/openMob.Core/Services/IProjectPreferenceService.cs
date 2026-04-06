using openMob.Core.Data.Entities;
using openMob.Core.Models;

namespace openMob.Core.Services;

/// <summary>
/// Manages per-project user preferences (e.g., default model, agent, thinking level, auto-accept).
/// </summary>
public interface IProjectPreferenceService
{
    /// <summary>
    /// Retrieves the project preference for the specified project.
    /// </summary>
    /// <param name="projectId">The external project identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The preference if found; null otherwise.</returns>
    Task<ProjectPreference?> GetAsync(string projectId, CancellationToken ct = default);

    /// <summary>
    /// Returns the stored preference for the project, or a new <see cref="ProjectPreference"/>
    /// populated with global defaults if none exists. Never returns null.
    /// Does not insert a row into the database when returning defaults.
    /// </summary>
    /// <param name="projectId">The external project identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored or default preference. Never null.</returns>
    Task<ProjectPreference> GetOrDefaultAsync(string projectId, CancellationToken ct = default);

    /// <summary>
    /// Sets or updates the default model for the specified project.
    /// Creates a new preference record if one does not exist.
    /// </summary>
    /// <param name="projectId">The external project identifier.</param>
    /// <param name="modelId">The model identifier in "providerId/modelId" format.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the preference was persisted successfully; <c>false</c> if persistence failed.</returns>
    Task<bool> SetDefaultModelAsync(string projectId, string modelId, CancellationToken ct = default);

    /// <summary>
    /// Clears the default model for the specified project (sets it to null).
    /// Creates a new preference record if one does not exist.
    /// </summary>
    /// <param name="projectId">The external project identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if persisted successfully; <c>false</c> if persistence failed.</returns>
    Task<bool> ClearDefaultModelAsync(string projectId, CancellationToken ct = default);

    /// <summary>
    /// Sets or updates the agent name for the specified project.
    /// Creates a new preference record if one does not exist.
    /// </summary>
    /// <param name="projectId">The external project identifier.</param>
    /// <param name="agentName">The agent name, or null for the default agent.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if persisted successfully; <c>false</c> if persistence failed.</returns>
    Task<bool> SetAgentAsync(string projectId, string? agentName, CancellationToken ct = default);

    /// <summary>
    /// Sets or updates the thinking level for the specified project.
    /// Creates a new preference record if one does not exist.
    /// </summary>
    /// <param name="projectId">The external project identifier.</param>
    /// <param name="level">The thinking level.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if persisted successfully; <c>false</c> if persistence failed.</returns>
    Task<bool> SetThinkingLevelAsync(string projectId, ThinkingLevel level, CancellationToken ct = default);

    /// <summary>
    /// Sets or updates the auto-accept setting for the specified project.
    /// Creates a new preference record if one does not exist.
    /// </summary>
    /// <param name="projectId">The external project identifier.</param>
    /// <param name="autoAccept">Whether auto-accept is enabled.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if persisted successfully; <c>false</c> if persistence failed.</returns>
    Task<bool> SetAutoAcceptAsync(string projectId, bool autoAccept, CancellationToken ct = default);

    /// <summary>
    /// Sets or updates the show-unhandled-SSE-events setting for the specified project.
    /// Creates a new preference record if one does not exist.
    /// </summary>
    /// <param name="projectId">The external project identifier.</param>
    /// <param name="value">Whether to show unhandled SSE event debug cards.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if persisted successfully; <c>false</c> if persistence failed.</returns>
    Task<bool> SetShowUnhandledSseEventsAsync(string projectId, bool value, CancellationToken ct = default);
}
