using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using openMob.Tests.Helpers;

namespace openMob.Tests.Data.Repositories;

/// <summary>
/// Unit tests for <see cref="ServerConnectionRepository"/>.
/// Uses in-memory SQLite to support raw SQL operations (ExecuteSqlRawAsync).
/// </summary>
public sealed class ServerConnectionRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly InMemoryServerCredentialStore _credentialStore;

    public ServerConnectionRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = TestDbContextFactory.Create(_connection);
        _credentialStore = new InMemoryServerCredentialStore();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    private ServerConnectionRepository CreateSut() => new(_context, _credentialStore);

    private async Task<ServerConnection> SeedConnectionAsync(
        string? id = null,
        string name = "Seeded Server",
        string host = "192.168.1.1",
        int port = 4096,
        string? username = null,
        bool isActive = false,
        bool discoveredViaMdns = false,
        DateTime? createdAt = null,
        DateTime? updatedAt = null)
    {
        var now = DateTime.UtcNow;
        var entity = new ServerConnection
        {
            Id = id ?? Ulid.NewUlid().ToString(),
            Name = name,
            Host = host,
            Port = port,
            Username = username,
            IsActive = isActive,
            DiscoveredViaMdns = discoveredViaMdns,
            CreatedAt = createdAt ?? now,
            UpdatedAt = updatedAt ?? now,
        };

        _context.ServerConnections.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    // ──────────────────────────────────────────────────────────────
    // GetAllAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_WhenNoConnections_ReturnsEmptyList()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WhenConnectionsExist_ReturnsAllOrderedByCreatedAt()
    {
        // Arrange
        var older = DateTime.UtcNow.AddMinutes(-10);
        var newer = DateTime.UtcNow;
        await SeedConnectionAsync(id: "conn-older", name: "Older", createdAt: older);
        await SeedConnectionAsync(id: "conn-newer", name: "Newer", createdAt: newer);
        var sut = CreateSut();

        // Act
        var result = await sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Older");
        result[1].Name.Should().Be("Newer");
    }

    [Fact]
    public async Task GetAllAsync_WhenPasswordExists_SetsHasPasswordTrue()
    {
        // Arrange
        var entity = await SeedConnectionAsync(id: "conn-with-pw");
        await _credentialStore.SavePasswordAsync(entity.Id, "secret123");
        var sut = CreateSut();

        // Act
        var result = await sut.GetAllAsync();

        // Assert
        result.Should().ContainSingle();
        result[0].HasPassword.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllAsync_WhenNoPasswordExists_SetsHasPasswordFalse()
    {
        // Arrange
        await SeedConnectionAsync(id: "conn-no-pw");
        var sut = CreateSut();

        // Act
        var result = await sut.GetAllAsync();

        // Assert
        result.Should().ContainSingle();
        result[0].HasPassword.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────
    // GetByIdAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WhenConnectionExists_ReturnsDto()
    {
        // Arrange
        var entity = await SeedConnectionAsync(id: "conn-1", name: "My Server", host: "10.0.0.1", port: 8080);
        var sut = CreateSut();

        // Act
        var result = await sut.GetByIdAsync(entity.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("conn-1");
        result.Name.Should().Be("My Server");
        result.Host.Should().Be("10.0.0.1");
        result.Port.Should().Be(8080);
    }

    [Fact]
    public async Task GetByIdAsync_WhenConnectionNotFound_ReturnsNull()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.GetByIdAsync("nonexistent-id");

        // Assert
        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────
    // GetActiveAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetActiveAsync_WhenActiveConnectionExists_ReturnsIt()
    {
        // Arrange
        await SeedConnectionAsync(id: "conn-inactive", isActive: false);
        var active = await SeedConnectionAsync(id: "conn-active", name: "Active Server", isActive: true);
        var sut = CreateSut();

        // Act
        var result = await sut.GetActiveAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(active.Id);
        result.Name.Should().Be("Active Server");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveAsync_WhenNoActiveConnection_ReturnsNull()
    {
        // Arrange
        await SeedConnectionAsync(id: "conn-1", isActive: false);
        await SeedConnectionAsync(id: "conn-2", isActive: false);
        var sut = CreateSut();

        // Act
        var result = await sut.GetActiveAsync();

        // Assert
        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────
    // AddAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_WhenValidDto_CreatesEntityWithNewUlid()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto(
            id: "should-be-ignored",
            name: "New Server",
            host: "10.0.0.5",
            port: 9090);
        var sut = CreateSut();

        // Act
        var result = await sut.AddAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBe("should-be-ignored");
        result.Id.Should().NotBeNullOrWhiteSpace();
        result.Name.Should().Be("New Server");
        result.Host.Should().Be("10.0.0.5");
        result.Port.Should().Be(9090);
    }

    [Fact]
    public async Task AddAsync_WhenValidDto_SetsCreatedAtAndUpdatedAt()
    {
        // Arrange
        var beforeAdd = DateTime.UtcNow;
        var dto = TestDataBuilder.CreateServerConnectionDto(name: "Timestamped Server");
        var sut = CreateSut();

        // Act
        var result = await sut.AddAsync(dto);

        // Assert
        result.CreatedAt.Should().BeOnOrAfter(beforeAdd);
        result.UpdatedAt.Should().BeOnOrAfter(beforeAdd);
        result.CreatedAt.Should().Be(result.UpdatedAt);
    }

    [Fact]
    public async Task AddAsync_WhenValidDto_PersistsEntityInDatabase()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto(name: "Persisted Server");
        var sut = CreateSut();

        // Act
        var result = await sut.AddAsync(dto);

        // Assert
        var entityInDb = await _context.ServerConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(sc => sc.Id == result.Id);
        entityInDb.Should().NotBeNull();
        entityInDb!.Name.Should().Be("Persisted Server");
    }

    // ──────────────────────────────────────────────────────────────
    // UpdateAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_WhenConnectionExists_UpdatesFields()
    {
        // Arrange
        var entity = await SeedConnectionAsync(
            id: "conn-update",
            name: "Old Name",
            host: "192.168.1.1",
            port: 4096,
            username: null,
            discoveredViaMdns: false);
        var updateDto = TestDataBuilder.CreateServerConnectionDto(
            id: entity.Id,
            name: "New Name",
            host: "10.0.0.99",
            port: 8888,
            username: "admin",
            discoveredViaMdns: true);
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateAsync(updateDto);

        // Assert
        result.Name.Should().Be("New Name");
        result.Host.Should().Be("10.0.0.99");
        result.Port.Should().Be(8888);
        result.Username.Should().Be("admin");
        result.DiscoveredViaMdns.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_WhenConnectionExists_UpdatesUpdatedAtTimestamp()
    {
        // Arrange
        var pastTime = DateTime.UtcNow.AddMinutes(-30);
        var entity = await SeedConnectionAsync(id: "conn-ts", updatedAt: pastTime);
        var updateDto = TestDataBuilder.CreateServerConnectionDto(id: entity.Id, name: "Updated");
        var sut = CreateSut();

        // Act
        var result = await sut.UpdateAsync(updateDto);

        // Assert
        result.UpdatedAt.Should().BeAfter(pastTime);
    }

    [Fact]
    public async Task UpdateAsync_WhenConnectionNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto(id: "nonexistent-id");
        var sut = CreateSut();

        // Act
        var act = async () => await sut.UpdateAsync(dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nonexistent-id*");
    }

    // ──────────────────────────────────────────────────────────────
    // DeleteAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_WhenConnectionExists_RemovesEntityAndReturnsTrue()
    {
        // Arrange
        var entity = await SeedConnectionAsync(id: "conn-delete");
        var sut = CreateSut();

        // Act
        var result = await sut.DeleteAsync(entity.Id);

        // Assert
        result.Should().BeTrue();
        var entityInDb = await _context.ServerConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(sc => sc.Id == entity.Id);
        entityInDb.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WhenConnectionExists_DeletesCredentialFromStore()
    {
        // Arrange
        var entity = await SeedConnectionAsync(id: "conn-cred-delete");
        await _credentialStore.SavePasswordAsync(entity.Id, "password123");
        var sut = CreateSut();

        // Act
        await sut.DeleteAsync(entity.Id);

        // Assert
        var password = await _credentialStore.GetPasswordAsync(entity.Id);
        password.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WhenConnectionNotFound_ReturnsFalse()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.DeleteAsync("nonexistent-id");

        // Assert
        result.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────
    // SetActiveAsync (single-active constraint — REQ-005)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetActiveAsync_WhenConnectionExists_SetsItActive()
    {
        // Arrange
        var entity = await SeedConnectionAsync(id: "conn-activate", isActive: false);
        var sut = CreateSut();

        // Act
        var result = await sut.SetActiveAsync(entity.Id);

        // Assert
        result.Should().BeTrue();
        var updated = await _context.ServerConnections
            .AsNoTracking()
            .FirstAsync(sc => sc.Id == entity.Id);
        updated.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SetActiveAsync_WhenOtherConnectionsActive_DeactivatesThem()
    {
        // Arrange
        var first = await SeedConnectionAsync(id: "conn-first", name: "First", isActive: true);
        var second = await SeedConnectionAsync(id: "conn-second", name: "Second", isActive: false);
        var sut = CreateSut();

        // Act
        var result = await sut.SetActiveAsync(second.Id);

        // Assert
        result.Should().BeTrue();

        // Reload from DB to verify — detach tracked entities first
        _context.ChangeTracker.Clear();

        var firstReloaded = await _context.ServerConnections
            .AsNoTracking()
            .FirstAsync(sc => sc.Id == first.Id);
        firstReloaded.IsActive.Should().BeFalse();

        var secondReloaded = await _context.ServerConnections
            .AsNoTracking()
            .FirstAsync(sc => sc.Id == second.Id);
        secondReloaded.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SetActiveAsync_WhenConnectionNotFound_ReturnsFalse()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.SetActiveAsync("nonexistent-id");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetActiveAsync_WhenConnectionNotFound_RollsBackDeactivation()
    {
        // Arrange
        var active = await SeedConnectionAsync(id: "conn-stays-active", isActive: true);
        var sut = CreateSut();

        // Act
        var result = await sut.SetActiveAsync("nonexistent-id");

        // Assert
        result.Should().BeFalse();

        // The previously active connection should remain active after rollback
        _context.ChangeTracker.Clear();
        var reloaded = await _context.ServerConnections
            .AsNoTracking()
            .FirstAsync(sc => sc.Id == active.Id);
        reloaded.IsActive.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────
    // GetDefaultModelAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDefaultModelAsync_WhenNoModelSet_ReturnsNull()
    {
        // Arrange
        var entity = await SeedConnectionAsync(id: "conn-no-model");
        var sut = CreateSut();

        // Act
        var result = await sut.GetDefaultModelAsync(entity.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDefaultModelAsync_WhenModelSet_ReturnsModelId()
    {
        // Arrange
        var entity = await SeedConnectionAsync(id: "conn-with-model");
        entity.DefaultModelId = "anthropic/claude-3-opus";
        _context.ServerConnections.Update(entity);
        await _context.SaveChangesAsync();

        var sut = CreateSut();

        // Act
        var result = await sut.GetDefaultModelAsync(entity.Id);

        // Assert
        result.Should().Be("anthropic/claude-3-opus");
    }

    [Fact]
    public async Task GetDefaultModelAsync_WhenServerNotFound_ReturnsNull()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.GetDefaultModelAsync("nonexistent-id");

        // Assert
        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────
    // SetDefaultModelAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetDefaultModelAsync_WhenServerExists_SetsModelIdAndReturnsTrue()
    {
        // Arrange
        var entity = await SeedConnectionAsync(id: "conn-set-model");
        var sut = CreateSut();

        // Act
        var result = await sut.SetDefaultModelAsync(entity.Id, "openai/gpt-4");

        // Assert
        result.Should().BeTrue();

        _context.ChangeTracker.Clear();
        var reloaded = await _context.ServerConnections
            .AsNoTracking()
            .FirstAsync(sc => sc.Id == entity.Id);
        reloaded.DefaultModelId.Should().Be("openai/gpt-4");
    }

    [Fact]
    public async Task SetDefaultModelAsync_WhenServerExists_UpdatesUpdatedAtTimestamp()
    {
        // Arrange
        var pastTime = DateTime.UtcNow.AddMinutes(-30);
        var entity = await SeedConnectionAsync(id: "conn-ts-model", updatedAt: pastTime);
        var sut = CreateSut();

        // Act
        await sut.SetDefaultModelAsync(entity.Id, "anthropic/claude-3-opus");

        // Assert
        _context.ChangeTracker.Clear();
        var reloaded = await _context.ServerConnections
            .AsNoTracking()
            .FirstAsync(sc => sc.Id == entity.Id);
        reloaded.UpdatedAt.Should().BeAfter(pastTime);
    }

    [Fact]
    public async Task SetDefaultModelAsync_WhenServerNotFound_ReturnsFalse()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.SetDefaultModelAsync("nonexistent-id", "anthropic/claude-3-opus");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetDefaultModelAsync_WhenCalledTwice_OverwritesPreviousModel()
    {
        // Arrange
        var entity = await SeedConnectionAsync(id: "conn-overwrite-model");
        var sut = CreateSut();

        // Act
        await sut.SetDefaultModelAsync(entity.Id, "anthropic/claude-3-opus");
        await sut.SetDefaultModelAsync(entity.Id, "openai/gpt-4");

        // Assert
        _context.ChangeTracker.Clear();
        var reloaded = await _context.ServerConnections
            .AsNoTracking()
            .FirstAsync(sc => sc.Id == entity.Id);
        reloaded.DefaultModelId.Should().Be("openai/gpt-4");
    }
}
