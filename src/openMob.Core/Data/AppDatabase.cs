using SQLite;
using openMob.Core.Data.Entities;

namespace openMob.Core.Data;

/// <summary>
/// sqlite-net-pcl implementation of <see cref="IAppDatabase"/>.
/// Opens a single <see cref="SQLiteAsyncConnection"/> and ensures all tables exist on startup.
/// Registered as a Singleton in the DI container — the connection is thread-safe and shared.
/// </summary>
internal sealed class AppDatabase : IAppDatabase
{
    private readonly IAppDataPathProvider _pathProvider;
    private SQLiteAsyncConnection? _connection;

    /// <summary>Initialises the database with the given path provider.</summary>
    /// <param name="pathProvider">Provides the platform-specific application data directory.</param>
    public AppDatabase(IAppDataPathProvider pathProvider)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        _pathProvider = pathProvider;
    }

    /// <inheritdoc />
    public SQLiteAsyncConnection Connection =>
        _connection ?? throw new InvalidOperationException(
            "AppDatabase has not been initialised. Call InitializeAsync() before accessing Connection.");

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        var dbPath = Path.Combine(_pathProvider.AppDataPath, "openmob.db");

        // storeDateTimeAsTicks: true — stores DateTime as 64-bit ticks for performance and precision.
        _connection = new SQLiteAsyncConnection(dbPath, storeDateTimeAsTicks: true);

        // CreateTableAsync is idempotent: creates the table if it does not exist,
        // or adds any missing columns via ALTER TABLE ADD COLUMN if it does.
        await _connection.CreateTableAsync<ServerConnection>().ConfigureAwait(false);
        await _connection.CreateTableAsync<ProjectPreference>().ConfigureAwait(false);
        await _connection.CreateTableAsync<AppState>().ConfigureAwait(false);
    }
}
