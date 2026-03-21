using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using openMob.Core.Services;
using openMob.Tests.Helpers;

namespace openMob.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AppStateService"/>.
/// Uses in-memory SQLite via <see cref="TestDbContextFactory"/> to test real EF Core
/// queries without touching the filesystem.
/// </summary>
public sealed class AppStateServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AppStateService _sut;

    public AppStateServiceTests()
    {
        // Open a shared in-memory SQLite connection that persists for the test lifetime.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Wire up the IServiceScopeFactory → IServiceScope → IServiceProvider → AppDbContext chain.
        // Each call to CreateScope() returns a fresh AppDbContext sharing the same in-memory connection.
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(_ =>
        {
            var db = TestDbContextFactory.Create(_connection);
            var scope = Substitute.For<IServiceScope>();
            var provider = Substitute.For<IServiceProvider>();
            provider.GetService(typeof(AppDbContext)).Returns(db);
            scope.ServiceProvider.Returns(provider);
            return scope;
        });

        _sut = new AppStateService(_scopeFactory);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    // ─── GetLastActiveProjectIdAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetLastActiveProjectIdAsync_WhenNoStateExists_ReturnsNull()
    {
        // Act
        var result = await _sut.GetLastActiveProjectIdAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLastActiveProjectIdAsync_WhenStateExists_ReturnsStoredProjectId()
    {
        // Arrange — seed the database with a known state entry
        using var seedDb = TestDbContextFactory.Create(_connection);
        seedDb.AppStates.Add(new AppState
        {
            Key = "LastActiveProjectId",
            Value = "proj-42",
        });
        await seedDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetLastActiveProjectIdAsync();

        // Assert
        result.Should().Be("proj-42");
    }

    [Fact]
    public async Task GetLastActiveProjectIdAsync_WhenOtherKeysExist_ReturnsNullForMissingKey()
    {
        // Arrange — seed with a different key
        using var seedDb = TestDbContextFactory.Create(_connection);
        seedDb.AppStates.Add(new AppState
        {
            Key = "SomeOtherKey",
            Value = "some-value",
        });
        await seedDb.SaveChangesAsync();

        // Act
        var result = await _sut.GetLastActiveProjectIdAsync();

        // Assert
        result.Should().BeNull();
    }

    // ─── SetLastActiveProjectIdAsync — creates new row ────────────────────────

    [Fact]
    public async Task SetLastActiveProjectIdAsync_WhenKeyDoesNotExist_CreatesNewRow()
    {
        // Act
        await _sut.SetLastActiveProjectIdAsync("proj-new");

        // Assert — verify via a fresh context that the row was persisted
        using var verifyDb = TestDbContextFactory.Create(_connection);
        var entry = await verifyDb.AppStates.FirstOrDefaultAsync(x => x.Key == "LastActiveProjectId");
        entry.Should().NotBeNull();
        entry!.Value.Should().Be("proj-new");
    }

    // ─── SetLastActiveProjectIdAsync — updates existing row ───────────────────

    [Fact]
    public async Task SetLastActiveProjectIdAsync_WhenKeyAlreadyExists_UpdatesExistingRow()
    {
        // Arrange — seed with an existing entry
        using var seedDb = TestDbContextFactory.Create(_connection);
        seedDb.AppStates.Add(new AppState
        {
            Key = "LastActiveProjectId",
            Value = "proj-old",
        });
        await seedDb.SaveChangesAsync();

        // Act
        await _sut.SetLastActiveProjectIdAsync("proj-updated");

        // Assert — verify the value was updated, not duplicated
        using var verifyDb = TestDbContextFactory.Create(_connection);
        var entries = await verifyDb.AppStates
            .Where(x => x.Key == "LastActiveProjectId")
            .ToListAsync();
        entries.Should().ContainSingle();
        entries[0].Value.Should().Be("proj-updated");
    }

    // ─── SetLastActiveProjectIdAsync — round-trip ─────────────────────────────

    [Fact]
    public async Task SetLastActiveProjectIdAsync_ThenGet_ReturnsPersistedValue()
    {
        // Act
        await _sut.SetLastActiveProjectIdAsync("proj-roundtrip");
        var result = await _sut.GetLastActiveProjectIdAsync();

        // Assert
        result.Should().Be("proj-roundtrip");
    }

    // ─── SetLastActiveProjectIdAsync — input validation ───────────────────────

    [Fact]
    public async Task SetLastActiveProjectIdAsync_WhenProjectIdIsNull_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _sut.SetLastActiveProjectIdAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetLastActiveProjectIdAsync_WhenProjectIdIsEmpty_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _sut.SetLastActiveProjectIdAsync(string.Empty);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetLastActiveProjectIdAsync_WhenProjectIdIsWhitespace_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _sut.SetLastActiveProjectIdAsync("   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task SetLastActiveProjectIdAsync_WhenProjectIdIsNullOrWhitespace_ThrowsArgumentException(string? projectId)
    {
        // Act
        var act = async () => await _sut.SetLastActiveProjectIdAsync(projectId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── Constructor — null guard ─────────────────────────────────────────────

    [Fact]
    public void Constructor_WhenScopeFactoryIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AppStateService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("scopeFactory");
    }
}
