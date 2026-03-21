namespace openMob.Core.Services;

/// <summary>
/// Service interface for reading and writing global application state from SQLite.
/// Provides typed accessors for well-known keys such as the last active project ID.
/// </summary>
/// <remarks>
/// Registered as Singleton in DI. Implementations must handle scoped <see cref="Data.AppDbContext"/>
/// access internally via <see cref="IServiceScopeFactory"/>.
/// </remarks>
public interface IAppStateService
{
    /// <summary>
    /// Gets the ID of the last active project, or <c>null</c> if no project has been persisted yet.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The project ID string, or <c>null</c>.</returns>
    Task<string?> GetLastActiveProjectIdAsync(CancellationToken ct = default);

    /// <summary>
    /// Persists the given project ID as the last active project.
    /// Performs an upsert: inserts a new row if the key does not exist, or updates the existing row.
    /// </summary>
    /// <param name="projectId">The project identifier to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetLastActiveProjectIdAsync(string projectId, CancellationToken ct = default);
}
