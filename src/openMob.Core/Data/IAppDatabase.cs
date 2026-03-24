using SQLite;

namespace openMob.Core.Data;

/// <summary>
/// Provides access to the application's SQLite database connection.
/// Registered as a Singleton in the DI container.
/// </summary>
public interface IAppDatabase
{
    /// <summary>
    /// Gets the shared <see cref="SQLiteAsyncConnection"/> instance.
    /// The connection is thread-safe and designed to be shared across all repositories and services.
    /// </summary>
    SQLiteAsyncConnection Connection { get; }

    /// <summary>
    /// Opens the database connection and ensures all tables exist.
    /// Must be called once at application startup before any repository or service accesses the DB.
    /// </summary>
    /// <returns>A task that completes when the database is ready.</returns>
    Task InitializeAsync();
}
