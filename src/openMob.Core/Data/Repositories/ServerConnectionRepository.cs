using Microsoft.EntityFrameworkCore;
using openMob.Core.Data.Entities;
using openMob.Core.Infrastructure.Dtos;
using openMob.Core.Infrastructure.Security;

namespace openMob.Core.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IServerConnectionRepository"/>.
/// Manages CRUD operations for server connections with credential coordination.
/// </summary>
/// <remarks>
/// <para>
/// The single-active constraint (<see cref="SetActiveAsync"/>) is enforced via an explicit
/// database transaction with a raw SQL UPDATE to deactivate all other connections.
/// </para>
/// <para>
/// The <see cref="ServerConnectionDto.HasPassword"/> field is computed by querying
/// <see cref="IServerCredentialStore"/> for each connection during entity-to-DTO mapping.
/// </para>
/// </remarks>
internal sealed class ServerConnectionRepository : IServerConnectionRepository
{
    private readonly AppDbContext _context;
    private readonly IServerCredentialStore _credentialStore;

    /// <summary>Initialises the repository with the given database context and credential store.</summary>
    /// <param name="context">The EF Core database context.</param>
    /// <param name="credentialStore">The secure credential store for server passwords.</param>
    public ServerConnectionRepository(AppDbContext context, IServerCredentialStore credentialStore)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(credentialStore);

        _context = context;
        _credentialStore = credentialStore;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ServerConnectionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.ServerConnections
            .AsNoTracking()
            .OrderBy(sc => sc.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var passwordTasks = entities.Select(e => _credentialStore.GetPasswordAsync(e.Id, cancellationToken)).ToArray();
        var passwords = await Task.WhenAll(passwordTasks).ConfigureAwait(false);

        var dtos = new List<ServerConnectionDto>(entities.Count);
        for (var i = 0; i < entities.Count; i++)
        {
            dtos.Add(MapToDto(entities[i], hasPassword: passwords[i] is not null));
        }

        return dtos.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<ServerConnectionDto?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _context.ServerConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(sc => sc.IsActive, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
            return null;

        var password = await _credentialStore.GetPasswordAsync(entity.Id, cancellationToken).ConfigureAwait(false);
        return MapToDto(entity, hasPassword: password is not null);
    }

    /// <inheritdoc />
    public async Task<ServerConnectionDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ServerConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(sc => sc.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
            return null;

        var password = await _credentialStore.GetPasswordAsync(entity.Id, cancellationToken).ConfigureAwait(false);
        return MapToDto(entity, hasPassword: password is not null);
    }

    /// <inheritdoc />
    public async Task<ServerConnectionDto> AddAsync(ServerConnectionDto dto, CancellationToken cancellationToken = default)
    {
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

        _context.ServerConnections.Add(entity);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var password = await _credentialStore.GetPasswordAsync(entity.Id, cancellationToken).ConfigureAwait(false);
        return MapToDto(entity, hasPassword: password is not null);
    }

    /// <inheritdoc />
    public async Task<ServerConnectionDto> UpdateAsync(ServerConnectionDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ServerConnections
            .FirstOrDefaultAsync(sc => sc.Id == dto.Id, cancellationToken)
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

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var password = await _credentialStore.GetPasswordAsync(entity.Id, cancellationToken).ConfigureAwait(false);
        return MapToDto(entity, hasPassword: password is not null);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.ServerConnections
            .FirstOrDefaultAsync(sc => sc.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
            return false;

        // Delete credential FIRST — idempotent (no-op if missing), so if the subsequent
        // DB delete fails, the worst case is a missing password (acceptable).
        // The reverse ordering would leave an orphaned secret if the app crashes after SaveChanges.
        await _credentialStore.DeletePasswordAsync(id, cancellationToken).ConfigureAwait(false);

        _context.ServerConnections.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> SetActiveAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // Deactivate all other connections via raw SQL for atomicity.
        // Uses WHERE Id != {id} to avoid the unnecessary write to the target row.
        await _context.Database
            .ExecuteSqlInterpolatedAsync($"UPDATE ServerConnections SET IsActive = 0 WHERE Id != {id}", cancellationToken)
            .ConfigureAwait(false);

        var entity = await _context.ServerConnections
            .FirstOrDefaultAsync(sc => sc.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        entity.IsActive = true;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

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
            HasPassword: hasPassword
        );
}
