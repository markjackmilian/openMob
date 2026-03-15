using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace openMob.Tests.Helpers;

/// <summary>Creates <see cref="AppDbContext"/> instances backed by in-memory SQLite for testing.</summary>
internal static class TestDbContextFactory
{
    /// <summary>
    /// Creates a new <see cref="AppDbContext"/> using the provided open SQLite connection.
    /// The database schema is created via <c>EnsureCreated</c>.
    /// </summary>
    /// <param name="connection">An already-opened <see cref="SqliteConnection"/>.</param>
    /// <returns>A configured <see cref="AppDbContext"/> ready for testing.</returns>
    public static AppDbContext Create(SqliteConnection connection)
    {
        var pathProvider = Substitute.For<IAppDataPathProvider>();
        pathProvider.AppDataPath.Returns(Path.GetTempPath());

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(pathProvider, options);
        context.Database.EnsureCreated();
        return context;
    }
}
