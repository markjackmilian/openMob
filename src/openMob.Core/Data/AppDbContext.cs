using Microsoft.EntityFrameworkCore;

namespace openMob.Core.Data;

/// <summary>
/// EF Core database context for openMob.
/// Uses SQLite stored in the platform application data directory.
/// </summary>
public sealed class AppDbContext : DbContext
{
    private readonly IAppDataPathProvider _pathProvider;

    /// <summary>Initialises the context with the given path provider and options.</summary>
    public AppDbContext(IAppDataPathProvider pathProvider, DbContextOptions<AppDbContext> options)
        : base(options)
    {
        _pathProvider = pathProvider;
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var dbPath = Path.Combine(_pathProvider.AppDataPath, "openmob.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Entity configurations will be added here as features are implemented
    }
}
