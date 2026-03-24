using System.Diagnostics;
using openMob.Core.Data.Entities;
using openMob.Core.Infrastructure.Dtos;
using openMob.Core.Infrastructure.Logging;
using openMob.Core.Infrastructure.Security;

namespace openMob.Core.Data.Repositories;

/// <summary>
/// sqlite-net-pcl implementation of <see cref="IServerConnectionRepository"/>.
/// Manages CRUD operations for server connections with credential coordination.
/// </summary>
/// <remarks>
/// <para>
/// The single-active constraint (<see cref="SetActiveAsync"/>) is enforced via
/// <see cref="SQLite.SQLiteAsyncConnection.RunInTransactionAsync"/> to atomically
/// deactivate all other connections and activate the target one.
/// </para>
/// <para>
/// The <see cref="ServerConnectionDto.HasPassword"/> field is computed by querying
/// <see cref="IServerCredentialStore"/> for each connection during entity-to-DTO mapping.
/// </para>
/// </remarks>
internal sealed class ServerConnectionRepository : IServerConnectionRepository
{
    private readonly IAppDatabase _db;
    private readonly IServerCredentialStore _credentialStore;

    /// <summary>Initialises the repository with the given database and credential store.</summary>
    /// <param name="db">The application database (Singleton).</param>
    /// <param name="credentialStore">The secure credential store for server passwords.</param>
    public ServerConnectionRepository(IAppDatabase db, IServerCredentialStore credentialStore)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(credentialStore);

        _db = db;
        _credentialStore = credentialStore;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ServerConnectionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
#endif
        var entities = await _db.Connection
            .Table<ServerConnection>()
            .OrderBy(sc => sc.CreatedAt)
            .ToListAsync()
            .ConfigureAwait(false);

        var passwordTasks = entities.Select(e => _credentialStore.GetPasswordAsync(e.Id, cancellationToken)).ToArray();
        var passwords = await Task.WhenAll(passwordTasks).ConfigureAwait(false);

        var dtos = new List<ServerConnectionDto>(entities.Count);
        for (var i = 0; i < entities.Count; i++)
        {
            dtos.Add(MapToDto(entities[i], hasPassword: passwords[i] is not null));
        }

        var result = dtos.AsReadOnly();
#if DEBUG
        sw.Stop();
        DebugLogger.LogDatabase("GetAll", "ServerConnection", null, sw.ElapsedMilliseconds);
#endif
        return result;
    }

    /// <inheritdoc />
    public async Task<ServerConnectionDto?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
#endif
        var entity = await _db.Connection
            .Table<ServerConnection>()
            .Where(sc => sc.IsActive)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (entity is null)
        {
#if DEBUG
            sw.Stop();
            DebugLogger.LogDatabase("Get", "ServerConnection", "active", sw.ElapsedMilliseconds);
#endif
            return null;
        }

        var password = await _credentialStore.GetPasswordAsync(entity.Id, cancellationToken).ConfigureAwait(false);
        var result = MapToDto(entity, hasPassword: password is not null);
#if DEBUG
        sw.Stop();
        DebugLogger.LogDatabase("Get", "ServerConnection", "active", sw.ElapsedMilliseconds);
#endif
        return result;
    }

    /// <inheritdoc />
    public async Task<ServerConnectionDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
#endif
        var entity = await _db.Connection
            .Table<ServerConnection>()
            .Where(sc => sc.Id == id)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (entity is null)
        {
#if DEBUG
            sw.Stop();
            DebugLogger.LogDatabase("Get", "ServerConnection", id, sw.ElapsedMilliseconds);
#endif
            return null;
        }

        var password = await _credentialStore.GetPasswordAsync(entity.Id, cancellationToken).ConfigureAwait(false);
        var result = MapToDto(entity, hasPassword: password is not null);
#if DEBUG
        sw.Stop();
        DebugLogger.LogDatabase("Get", "ServerConnection", id, sw.ElapsedMilliseconds);
#endif
        return result;
    }

    /// <inheritdoc />
    public async Task<ServerConnectionDto> AddAsync(ServerConnectionDto dto, CancellationToken cancellationToken = default)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
#endif
        var now = DateTime.UtcNow;

        var entity = new ServerConnection
        {
            Id = Ulid.NewUlid().ToString(),
            Name = dto.Name,
            Host = dto.Host,
            Port = dto.Port,
            Username = dto.Username,
            IsActive = dto.IsActive,
            DiscoveredViaMdns = dto.DiscoveredViaMdns,
            UseHttps = dto.UseHttps,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _db.Connection.InsertAsync(entity).ConfigureAwait(false);

        var password = await _credentialStore.GetPasswordAsync(entity.Id, cancellationToken).ConfigureAwait(false);
        var result = MapToDto(entity, hasPassword: password is not null);
#if DEBUG
        sw.Stop();
        DebugLogger.LogDatabase("Add", "ServerConnection", entity.Id, sw.ElapsedMilliseconds);
#endif
        return result;
    }

    /// <inheritdoc />
    public async Task<ServerConnectionDto> UpdateAsync(ServerConnectionDto dto, CancellationToken cancellationToken = default)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
#endif
        var entity = await _db.Connection
            .Table<ServerConnection>()
            .Where(sc => sc.Id == dto.Id)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (entity is null)
            throw new InvalidOperationException($"Server connection with ID '{dto.Id}' was not found.");

        // IsActive is intentionally excluded — it must only change via SetActiveAsync
        // to enforce the single-active constraint within a transaction.
        entity.Name = dto.Name;
        entity.Host = dto.Host;
        entity.Port = dto.Port;
        entity.Username = dto.Username;
        entity.DiscoveredViaMdns = dto.DiscoveredViaMdns;
        entity.UseHttps = dto.UseHttps;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.Connection.UpdateAsync(entity).ConfigureAwait(false);

        var password = await _credentialStore.GetPasswordAsync(entity.Id, cancellationToken).ConfigureAwait(false);
        var result = MapToDto(entity, hasPassword: password is not null);
#if DEBUG
        sw.Stop();
        DebugLogger.LogDatabase("Update", "ServerConnection", dto.Id, sw.ElapsedMilliseconds);
#endif
        return result;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
#endif
        var entity = await _db.Connection
            .Table<ServerConnection>()
            .Where(sc => sc.Id == id)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (entity is null)
        {
#if DEBUG
            sw.Stop();
            DebugLogger.LogDatabase("Delete", "ServerConnection", id, sw.ElapsedMilliseconds);
#endif
            return false;
        }

        // Delete credential FIRST — idempotent (no-op if missing), so if the subsequent
        // DB delete fails, the worst case is a missing password (acceptable).
        // The reverse ordering would leave an orphaned secret if the app crashes after the DB delete.
        await _credentialStore.DeletePasswordAsync(id, cancellationToken).ConfigureAwait(false);

        await _db.Connection.DeleteAsync(entity).ConfigureAwait(false);

#if DEBUG
        sw.Stop();
        DebugLogger.LogDatabase("Delete", "ServerConnection", id, sw.ElapsedMilliseconds);
#endif
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> SetActiveAsync(string id, CancellationToken cancellationToken = default)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
#endif
        // Use RunInTransactionAsync to atomically deactivate all other connections
        // and activate the target one. If the target is not found, the transaction is
        // rolled back and previously active connections remain active.
        var found = false;
        string? activatedName = null;

        try
        {
            await _db.Connection.RunInTransactionAsync(conn =>
            {
                // Deactivate all connections except the target.
                conn.Execute(
                    "UPDATE ServerConnections SET IsActive = 0 WHERE Id != ?",
                    id);

                // Find and activate the target connection.
                var entity = conn.Table<ServerConnection>()
                    .Where(sc => sc.Id == id)
                    .FirstOrDefault();

                if (entity is null)
                {
                    // Target not found — throw to abort the transaction and roll back.
                    throw new InvalidOperationException($"__rollback__:{id}");
                }

                entity.IsActive = true;
                entity.UpdatedAt = DateTime.UtcNow;
                conn.Update(entity);

                found = true;
                activatedName = entity.Name;
            }).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("__rollback__:", StringComparison.Ordinal))
        {
            // Sentinel exception used to abort the transaction — not a real error.
            // The transaction was rolled back; previously active connections remain active.
            found = false;
        }

#if DEBUG
        sw.Stop();
        DebugLogger.LogDatabase("SetActive", "ServerConnection", id, sw.ElapsedMilliseconds);
        if (found)
            DebugLogger.LogConnection("server_changed", $"id={id} name={activatedName}");
#endif
        return found;
    }

    /// <inheritdoc />
    public async Task<string?> GetDefaultModelAsync(string serverId, CancellationToken cancellationToken = default)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
#endif
        var entity = await _db.Connection
            .Table<ServerConnection>()
            .Where(sc => sc.Id == serverId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        var result = entity?.DefaultModelId;
#if DEBUG
        sw.Stop();
        DebugLogger.LogDatabase("GetDefaultModel", "ServerConnection", serverId, sw.ElapsedMilliseconds);
#endif
        return result;
    }

    /// <inheritdoc />
    public async Task<bool> SetDefaultModelAsync(string serverId, string modelId, CancellationToken cancellationToken = default)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
#endif
        var entity = await _db.Connection
            .Table<ServerConnection>()
            .Where(sc => sc.Id == serverId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (entity is null)
        {
#if DEBUG
            sw.Stop();
            DebugLogger.LogDatabase("SetDefaultModel", "ServerConnection", serverId, sw.ElapsedMilliseconds);
#endif
            return false;
        }

        entity.DefaultModelId = modelId;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.Connection.UpdateAsync(entity).ConfigureAwait(false);

#if DEBUG
        sw.Stop();
        DebugLogger.LogDatabase("SetDefaultModel", "ServerConnection", serverId, sw.ElapsedMilliseconds);
#endif
        return true;
    }

    /// <summary>Maps a <see cref="ServerConnection"/> entity to a <see cref="ServerConnectionDto"/>.</summary>
    /// <param name="entity">The entity to map.</param>
    /// <param name="hasPassword">Whether a password exists in secure storage for this connection.</param>
    /// <returns>The mapped DTO.</returns>
    private static ServerConnectionDto MapToDto(ServerConnection entity, bool hasPassword) =>
        new(
            Id: entity.Id,
            Name: entity.Name,
            Host: entity.Host,
            Port: entity.Port,
            Username: entity.Username,
            IsActive: entity.IsActive,
            DiscoveredViaMdns: entity.DiscoveredViaMdns,
            UseHttps: entity.UseHttps,
            CreatedAt: entity.CreatedAt,
            UpdatedAt: entity.UpdatedAt,
            HasPassword: hasPassword,
            DefaultModelId: entity.DefaultModelId
        );
}
