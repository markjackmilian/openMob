using openMob.Core.Infrastructure.Dtos;

namespace openMob.Core.Data.Repositories;

/// <summary>
/// Repository for CRUD operations on server connections.
/// </summary>
/// <remarks>
/// All methods return <see cref="ServerConnectionDto"/> instances (never raw entities).
/// The <see cref="ServerConnectionDto.HasPassword"/> field is computed by checking
/// <see cref="Infrastructure.Security.IServerCredentialStore"/> for each connection.
/// </remarks>
public interface IServerConnectionRepository
{
    /// <summary>Gets all server connections ordered by creation date.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of all server connection DTOs.</returns>
    Task<IReadOnlyList<ServerConnectionDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the currently active server connection, or null if none is active.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active server connection DTO, or <c>null</c>.</returns>
    Task<ServerConnectionDto?> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a server connection by its ID, or null if not found.</summary>
    /// <param name="id">The unique identifier of the server connection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The server connection DTO, or <c>null</c> if not found.</returns>
    Task<ServerConnectionDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Adds a new server connection.</summary>
    /// <param name="dto">The server connection data. The <see cref="ServerConnectionDto.Id"/> is ignored; a new ULID is generated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created server connection DTO with the generated ID.</returns>
    Task<ServerConnectionDto> AddAsync(ServerConnectionDto dto, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing server connection.</summary>
    /// <param name="dto">The server connection data with updated fields.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated server connection DTO.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the connection is not found.</exception>
    /// <remarks>
    /// The <see cref="ServerConnectionDto.IsActive"/> property is not updated by this method.
    /// Use <see cref="SetActiveAsync"/> to change the active connection, which enforces
    /// the single-active constraint within a transaction.
    /// </remarks>
    Task<ServerConnectionDto> UpdateAsync(ServerConnectionDto dto, CancellationToken cancellationToken = default);

    /// <summary>Deletes a server connection and its associated credentials. Returns true if found and deleted.</summary>
    /// <param name="id">The unique identifier of the server connection to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the connection was found and deleted; <c>false</c> otherwise.</returns>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the specified connection as the active one.
    /// All other connections are deactivated within the same transaction.
    /// </summary>
    /// <param name="id">The unique identifier of the connection to activate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the connection was found and activated; <c>false</c> otherwise.</returns>
    Task<bool> SetActiveAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Gets the default model ID for the specified server.</summary>
    /// <param name="serverId">The unique identifier of the server connection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The default model ID, or <c>null</c> if not set or the server is not found.</returns>
    Task<string?> GetDefaultModelAsync(string serverId, CancellationToken cancellationToken = default);

    /// <summary>Sets the default model ID for the specified server.</summary>
    /// <param name="serverId">The unique identifier of the server connection.</param>
    /// <param name="modelId">The model ID to set as default (format: "providerId/modelId").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the server was found and updated; <c>false</c> otherwise.</returns>
    Task<bool> SetDefaultModelAsync(string serverId, string modelId, CancellationToken cancellationToken = default);
}
