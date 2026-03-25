using openMob.Core.Infrastructure.Http.Dtos.Opencode;

namespace openMob.Core.Services;

/// <summary>
/// Service interface for project operations.
/// Wraps <see cref="Infrastructure.Http.IOpencodeApiClient"/> project methods
/// with a cleaner API surface and error handling.
/// </summary>
public interface IProjectService
{
    /// <summary>Gets all projects known to the opencode server.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of all projects, or an empty list on failure.</returns>
    Task<IReadOnlyList<ProjectDto>> GetAllProjectsAsync(CancellationToken ct = default);

    /// <summary>Gets the currently active project on the server.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The current project, or <c>null</c> if none is set or on failure.</returns>
    Task<ProjectDto?> GetCurrentProjectAsync(CancellationToken ct = default);

    /// <summary>Gets a project by its ID (client-side filter from all projects).</summary>
    /// <param name="id">The project identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching project, or <c>null</c> if not found.</returns>
    Task<ProjectDto?> GetProjectByIdAsync(string id, CancellationToken ct = default);

    /// <summary>Gets a project by its worktree path (client-side filter from all projects).</summary>
    /// <param name="worktree">The project worktree path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching project, or <c>null</c> if not found.</returns>
    Task<ProjectDto?> GetProjectByWorktreeAsync(string worktree, CancellationToken ct = default);

    /// <summary>
    /// Gets an existing project for the specified worktree or creates a new project context
    /// on the server when none exists yet.
    /// </summary>
    /// <param name="worktree">The server directory path selected by the user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching or newly registered project, or <c>null</c> on failure.</returns>
    Task<ProjectDto?> EnsureProjectForWorktreeAsync(string worktree, CancellationToken ct = default);
}
