using Microsoft.EntityFrameworkCore;
using openMob.Core.Data.Entities;
using openMob.Core.Models;

namespace openMob.Core.Data;

/// <summary>
/// EF Core database context for openMob.
/// Uses SQLite stored in the platform application data directory.
/// </summary>
public sealed class AppDbContext : DbContext
{
    private readonly IAppDataPathProvider _pathProvider;

    /// <summary>Gets or sets the server connections table.</summary>
    public DbSet<ServerConnection> ServerConnections { get; set; } = null!;

    /// <summary>Gets or sets the per-project user preferences table.</summary>
    public DbSet<ProjectPreference> ProjectPreferences { get; set; } = null!;

    /// <summary>Gets or sets the global application state key-value table.</summary>
    public DbSet<AppState> AppStates { get; set; } = null!;

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

        modelBuilder.Entity<ServerConnection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Host).IsRequired();
            entity.Property(e => e.Port).HasDefaultValue(4096);
            entity.Property(e => e.IsActive).HasDefaultValue(false);
            entity.Property(e => e.DiscoveredViaMdns).HasDefaultValue(false);
            entity.Property(e => e.UseHttps).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt);
            entity.Property(e => e.UpdatedAt);
            entity.Property(e => e.DefaultModelId).HasMaxLength(500);
            entity.HasIndex(e => e.IsActive);
        });

        modelBuilder.Entity<AppState>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Value).HasMaxLength(500);
        });

        modelBuilder.Entity<ProjectPreference>(entity =>
        {
            entity.HasKey(e => e.ProjectId);
            entity.Property(e => e.ProjectId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.DefaultModelId).HasMaxLength(500);
            entity.Property(e => e.AgentName).HasMaxLength(500);
            entity.Property(e => e.ThinkingLevel)
                .HasConversion<int>()
                .HasDefaultValue(ThinkingLevel.Medium);
            entity.Property(e => e.AutoAccept).HasDefaultValue(false);
        });
    }
}
