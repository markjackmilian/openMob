using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace openMob.Core.Data;

/// <summary>
/// Design-time factory used by EF Core tooling (migrations, compiled model generation).
/// Not used at runtime — the real context is resolved via DI with <see cref="IAppDataPathProvider"/>.
/// </summary>
internal sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <inheritdoc />
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;

        return new AppDbContext(new DesignTimePathProvider(), options);
    }

    /// <summary>Stub path provider used only during design-time tooling.</summary>
    private sealed class DesignTimePathProvider : IAppDataPathProvider
    {
        /// <inheritdoc />
        public string AppDataPath => Path.GetTempPath();
    }
}
