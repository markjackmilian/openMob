using openMob.Core.Infrastructure.Http.Dtos.Opencode;

namespace openMob.Core.Services;

/// <summary>
/// Service interface for session operations.
/// Wraps <see cref="Infrastructure.Http.IOpencodeApiClient"/> session methods
/// with a cleaner API surface and error handling.
/// </summary>
public interface ISessionService
{
    /// <summary>Gets all sessions from the opencode server.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of all sessions, or an empty list on failure.</returns>
    Task<IReadOnlyList<SessionDto>> GetAllSessionsAsync(CancellationToken ct = default);

    /// <summary>Gets sessions filtered by project ID (client-side filter).</summary>
    /// <param name="projectId">The project identifier to filter by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Sessions belonging to the specified project, ordered by last updated descending.</returns>
    Task<IReadOnlyList<SessionDto>> GetSessionsByProjectAsync(string projectId, CancellationToken ct = default);

    /// <summary>Gets a single session by its ID.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The session, or <c>null</c> if not found or on failure.</returns>
    Task<SessionDto?> GetSessionAsync(string id, CancellationToken ct = default);

    /// <summary>Creates a new session on the server.</summary>
    /// <param name="title">An optional display title for the new session.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created session, or <c>null</c> on failure.</returns>
    Task<SessionDto?> CreateSessionAsync(string? title, CancellationToken ct = default);

    /// <summary>Creates a new session for the specified project.</summary>
    /// <param name="projectId">The project identifier to create the session for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created session DTO.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the API call fails. The message is user-readable and suitable for display.
    /// </exception>
    Task<SessionDto> CreateSessionForProjectAsync(string projectId, CancellationToken ct = default);

    /// <summary>Updates the title of an existing session.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="newTitle">The new title.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the update succeeded; <c>false</c> otherwise.</returns>
    Task<bool> UpdateSessionTitleAsync(string id, string newTitle, CancellationToken ct = default);

    /// <summary>Deletes a session from the server.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the deletion succeeded; <c>false</c> otherwise.</returns>
    Task<bool> DeleteSessionAsync(string id, CancellationToken ct = default);

    /// <summary>Forks a session from the latest message.</summary>
    /// <param name="id">The session identifier to fork from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly forked session, or <c>null</c> on failure.</returns>
    Task<SessionDto?> ForkSessionAsync(string id, CancellationToken ct = default);

    /// <summary>Gets the most recently updated session for a given project.</summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The most recent session, or <c>null</c> if no sessions exist for the project.</returns>
    Task<SessionDto?> GetLastSessionForProjectAsync(string projectId, CancellationToken ct = default);
}
