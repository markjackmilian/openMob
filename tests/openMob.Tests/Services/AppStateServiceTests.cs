using SQLite;
using SQLitePCL;
using openMob.Core.Services;

namespace openMob.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AppStateService"/>.
/// Uses sqlite-net-pcl with an in-memory SQLite connection for fast, isolated tests.
/// </summary>
public sealed class AppStateServiceTests : IAsyncLifetime
{
    private string _dbPath = null!;
    private SQLiteAsyncConnection _connection = null!;
    private IAppDatabase _appDatabase = null!;
    private AppStateService _sut = null!;

    /// <summary>Sets up a fresh SQLite database file before each test.</summary>
    public async Task InitializeAsync()
    {
        // Initialise the native SQLite bindings (idempotent — safe to call multiple times).
        Batteries_V2.Init();

        // Use a unique temp file per test to avoid cross-test interference.
        _dbPath = Path.Combine(Path.GetTempPath(), $"openMob_test_{Guid.NewGuid():N}.db");
        _connection = new SQLiteAsyncConnection(_dbPath, storeDateTimeAsTicks: true);
        await _connection.CreateTableAsync<AppState>();

        _appDatabase = Substitute.For<IAppDatabase>();
        _appDatabase.Connection.Returns(_connection);

        _sut = new AppStateService(_appDatabase);
    }

    /// <summary>Closes the connection and deletes the temp DB file after each test.</summary>
    public async Task DisposeAsync()
    {
        await _connection.CloseAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
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
        await _connection.InsertAsync(new AppState
        {
            Key = "LastActiveProjectId",
            Value = "proj-42",
        });

        // Act
        var result = await _sut.GetLastActiveProjectIdAsync();

        // Assert
        result.Should().Be("proj-42");
    }

    [Fact]
    public async Task GetLastActiveProjectIdAsync_WhenOtherKeysExist_ReturnsNullForMissingKey()
    {
        // Arrange — seed with a different key
        await _connection.InsertAsync(new AppState
        {
            Key = "SomeOtherKey",
            Value = "some-value",
        });

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

        // Assert — verify the row was persisted
        var entry = await _connection
            .Table<AppState>()
            .Where(x => x.Key == "LastActiveProjectId")
            .FirstOrDefaultAsync();
        entry.Should().NotBeNull();
        entry!.Value.Should().Be("proj-new");
    }

    // ─── SetLastActiveProjectIdAsync — updates existing row ───────────────────

    [Fact]
    public async Task SetLastActiveProjectIdAsync_WhenKeyAlreadyExists_UpdatesExistingRow()
    {
        // Arrange — seed with an existing entry
        await _connection.InsertAsync(new AppState
        {
            Key = "LastActiveProjectId",
            Value = "proj-old",
        });

        // Act
        await _sut.SetLastActiveProjectIdAsync("proj-updated");

        // Assert — verify the value was updated, not duplicated
        var entries = await _connection
            .Table<AppState>()
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
    public void Constructor_WhenDbIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AppStateService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("db");
    }
}
