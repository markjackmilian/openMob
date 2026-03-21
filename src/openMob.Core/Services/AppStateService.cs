using Microsoft.EntityFrameworkCore;
using openMob.Core.Data;
using openMob.Core.Data.Entities;

namespace openMob.Core.Services;

/// <summary>
/// Reads and writes global application state from the SQLite <c>AppStates</c> table.
/// </summary>
/// <remarks>
/// <para>
/// Registered as Singleton in DI, but <see cref="AppDbContext"/> is Scoped.
/// To avoid a captive dependency, this service injects <see cref="IServiceScopeFactory"/>
/// and creates a new scope for each database operation.
/// </para>
/// <para>
/// All methods use <c>ConfigureAwait(false)</c> because this is a pure service layer class
/// with no UI thread affinity.
/// </para>
/// </remarks>
internal sealed class AppStateService : IAppStateService
{
    private const string LastActiveProjectIdKey = "LastActiveProjectId";

    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>Initialises the service with the required scope factory.</summary>
    /// <param name="scopeFactory">Factory for creating DI scopes to resolve <see cref="AppDbContext"/>.</param>
    public AppStateService(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public async Task<string?> GetLastActiveProjectIdAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entry = await db.AppStates
            .FirstOrDefaultAsync(x => x.Key == LastActiveProjectIdKey, ct)
            .ConfigureAwait(false);

        return entry?.Value;
    }

    /// <inheritdoc />
    public async Task SetLastActiveProjectIdAsync(string projectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.AppStates
            .FirstOrDefaultAsync(x => x.Key == LastActiveProjectIdKey, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            existing.Value = projectId;
        }
        else
        {
            db.AppStates.Add(new AppState
            {
                Key = LastActiveProjectIdKey,
                Value = projectId,
            });
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
