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

        var passwordTasks = entities.Select(e => _credentialStore.GetPasswordAsync(e.Id)).ToArray();
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

        var password = await _credentialStore.GetPasswordAsync(entity.Id).ConfigureAwait(false);
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

        var password = await _credentialStore.GetPasswordAsync(entity.Id).ConfigureAwait(false);
        return MapToDto(entity, hasPassword: password is not null);
    }

    /// <inheritdoc />
    public async Task<ServerConnectionDto> AddAsync(ServerConnectionDto dto, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var entity = new ServerConnection
        {
            Id = Ulid.NewUlid().ToString(),
            Name = dto.Name,
            Host = dto.Host,
            Port = dto.Port,
            Username = dto.Username,
            IsActive = dto.IsActive,
            DiscoveredViaMdns = dto.DiscoveredViaMdns,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _context.ServerConnections.Add(entity);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var password = await _credentialStore.GetPasswordAsync(entity.Id).ConfigureAwait(false);
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

        entity.Name = dto.Name;
        entity.Host = dto.Host;
        entity.Port = dto.Port;
        entity.Username = dto.Username;
        entity.DiscoveredViaMdns = dto.DiscoveredViaMdns;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var password = await _credentialStore.GetPasswordAsync(entity.Id).ConfigureAwait(false);
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

        _context.ServerConnections.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _credentialStore.DeletePasswordAsync(id).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> SetActiveAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // Deactivate all currently active connections via raw SQL for atomicity
        await _context.Database
            .ExecuteSqlRawAsync("UPDATE ServerConnections SET IsActive = 0 WHERE IsActive = 1", cancellationToken)
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
        entity.UpdatedAt = DateTimeOffset.UtcNow;

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
            CreatedAt: entity.CreatedAt,
            UpdatedAt: entity.UpdatedAt,
            HasPassword: hasPassword
        );
}
