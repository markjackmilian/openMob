using openMob.Core.Data.Entities;

namespace openMob.Core.Services;

/// <summary>
/// Manages per-project user preferences (e.g., default model).
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
    /// Sets or updates the default model for the specified project.
    /// Creates a new preference record if one does not exist.
    /// </summary>
    /// <param name="projectId">The external project identifier.</param>
    /// <param name="modelId">The model identifier in "providerId/modelId" format.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetDefaultModelAsync(string projectId, string modelId, CancellationToken ct = default);
}
