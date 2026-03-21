using openMob.Core.Infrastructure.Http.Dtos.Opencode;

namespace openMob.Core.Services;

/// <summary>
/// Manages the client-side active project state.
/// On first access, initialises from the server's current project via <see cref="IProjectService"/>.
/// When the user switches projects in the app, stores the override in memory
/// and publishes an <see cref="Messages.ActiveProjectChangedMessage"/> via
/// <see cref="CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger"/>.
/// </summary>
/// <remarks>
/// Registered as Singleton — the active project state is global and persists for the app lifetime.
/// </remarks>
public interface IActiveProjectService
{
    /// <summary>
    /// Gets the currently active project. On the first call, initialises from the server
    /// via <see cref="IProjectService.GetCurrentProjectAsync"/>. Subsequent calls return the
    /// cached value (or the override if <see cref="SetActiveProjectAsync"/> was called).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The active project DTO, or <c>null</c> if none is set.</returns>
    Task<ProjectDto?> GetActiveProjectAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets the active project to the specified project ID. Fetches the full <see cref="ProjectDto"/>
    /// via <see cref="IProjectService.GetProjectByIdAsync"/>, caches it, and publishes
    /// an <see cref="Messages.ActiveProjectChangedMessage"/>.
    /// </summary>
    /// <param name="projectId">The project identifier to activate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the project was found and activated; <c>false</c> otherwise.</returns>
    Task<bool> SetActiveProjectAsync(string projectId, CancellationToken ct = default);

    /// <summary>
    /// Gets the worktree path of the currently cached active project, or <c>null</c> if no project
    /// has been resolved yet. This method is synchronous and never makes HTTP calls — it only
    /// reads the in-memory cached value set by <see cref="GetActiveProjectAsync"/> or
    /// <see cref="SetActiveProjectAsync"/>.
    /// </summary>
    /// <returns>The worktree path, or <c>null</c> if no active project is cached.</returns>
    string? GetCachedWorktree();
}
