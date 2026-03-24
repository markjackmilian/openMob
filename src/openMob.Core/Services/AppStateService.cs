using openMob.Core.Data;
using openMob.Core.Data.Entities;

namespace openMob.Core.Services;

/// <summary>
/// Reads and writes global application state from the SQLite <c>AppStates</c> table.
/// </summary>
/// <remarks>
/// <para>
/// Registered as Singleton in DI. Injects <see cref="IAppDatabase"/> directly (also Singleton),
/// eliminating the captive dependency problem that previously required <c>IServiceScopeFactory</c>.
/// </para>
/// <para>
/// All methods use <c>ConfigureAwait(false)</c> because this is a pure service layer class
/// with no UI thread affinity.
/// </para>
/// </remarks>
internal sealed class AppStateService : IAppStateService
{
    private const string LastActiveProjectIdKey = "LastActiveProjectId";

    private readonly IAppDatabase _db;

    /// <summary>Initialises the service with the required database.</summary>
    /// <param name="db">The application database (Singleton).</param>
    public AppStateService(IAppDatabase db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public async Task<string?> GetLastActiveProjectIdAsync(CancellationToken ct = default)
    {
        var entry = await _db.Connection
            .Table<AppState>()
            .Where(x => x.Key == LastActiveProjectIdKey)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        return entry?.Value;
    }

    /// <inheritdoc />
    public async Task SetLastActiveProjectIdAsync(string projectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        var existing = await _db.Connection
            .Table<AppState>()
            .Where(x => x.Key == LastActiveProjectIdKey)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (existing is not null)
        {
            existing.Value = projectId;
            await _db.Connection.UpdateAsync(existing).ConfigureAwait(false);
        }
        else
        {
            await _db.Connection.InsertAsync(new AppState
            {
                Key = LastActiveProjectIdKey,
                Value = projectId,
            }).ConfigureAwait(false);
        }
    }
}
