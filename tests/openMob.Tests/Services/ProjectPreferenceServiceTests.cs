using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using openMob.Core.Models;
using openMob.Core.Services;
using openMob.Tests.Helpers;

namespace openMob.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ProjectPreferenceService"/>.
/// Uses in-memory SQLite for persistence tests and validates argument guards.
/// </summary>
public sealed class ProjectPreferenceServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly ProjectPreferenceService _sut;

    public ProjectPreferenceServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = TestDbContextFactory.Create(_connection);
        _sut = new ProjectPreferenceService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Close();
        _connection.Dispose();
    }

    // ─── GetAsync — happy path ────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_WhenPreferenceExists_ReturnsPreference()
    {
        // Arrange
        var preference = new ProjectPreference
        {
            ProjectId = "proj-1",
            DefaultModelId = "anthropic/claude-sonnet-4-5",
        };
        _context.ProjectPreferences.Add(preference);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAsync("proj-1");

        // Assert
        result.Should().NotBeNull();
        result!.ProjectId.Should().Be("proj-1");
        result.DefaultModelId.Should().Be("anthropic/claude-sonnet-4-5");
    }

    // ─── GetAsync — not found ─────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_WhenPreferenceDoesNotExist_ReturnsNull()
    {
        // Act
        var result = await _sut.GetAsync("nonexistent-project");

        // Assert
        result.Should().BeNull();
    }

    // ─── GetAsync — argument validation ───────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAsync_WhenProjectIdIsNullOrWhiteSpace_ThrowsArgumentException(string? projectId)
    {
        // Act
        var act = async () => await _sut.GetAsync(projectId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── SetDefaultModelAsync — create new ────────────────────────────────────

    [Fact]
    public async Task SetDefaultModelAsync_WhenNoPreferenceExists_CreatesNewPreference()
    {
        // Act
        await _sut.SetDefaultModelAsync("proj-new", "openai/gpt-4");

        // Assert
        var saved = await _context.ProjectPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == "proj-new");
        saved.Should().NotBeNull();
        saved!.DefaultModelId.Should().Be("openai/gpt-4");
    }

    // ─── SetDefaultModelAsync — update existing ───────────────────────────────

    [Fact]
    public async Task SetDefaultModelAsync_WhenPreferenceExists_UpdatesDefaultModelId()
    {
        // Arrange
        _context.ProjectPreferences.Add(new ProjectPreference
        {
            ProjectId = "proj-existing",
            DefaultModelId = "anthropic/claude-3-opus",
        });
        await _context.SaveChangesAsync();

        // Act
        await _sut.SetDefaultModelAsync("proj-existing", "openai/gpt-4o");

        // Assert
        _context.ChangeTracker.Clear();
        var updated = await _context.ProjectPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == "proj-existing");
        updated.Should().NotBeNull();
        updated!.DefaultModelId.Should().Be("openai/gpt-4o");
    }

    // ─── SetDefaultModelAsync — argument validation ───────────────────────────

    [Theory]
    [InlineData(null, "anthropic/claude-3-opus")]
    [InlineData("", "anthropic/claude-3-opus")]
    [InlineData("   ", "anthropic/claude-3-opus")]
    public async Task SetDefaultModelAsync_WhenProjectIdIsNullOrWhiteSpace_ThrowsArgumentException(
        string? projectId, string modelId)
    {
        // Act
        var act = async () => await _sut.SetDefaultModelAsync(projectId!, modelId);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("proj-1", null)]
    [InlineData("proj-1", "")]
    [InlineData("proj-1", "   ")]
    public async Task SetDefaultModelAsync_WhenModelIdIsNullOrWhiteSpace_ThrowsArgumentException(
        string projectId, string? modelId)
    {
        // Act
        var act = async () => await _sut.SetDefaultModelAsync(projectId, modelId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── Constructor — null guard ─────────────────────────────────────────────

    [Fact]
    public void Constructor_WhenDbContextIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ProjectPreferenceService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // ─── GetOrDefaultAsync — happy path ──────────────────────────────────────

    [Fact]
    public async Task GetOrDefaultAsync_WhenPreferenceExists_ReturnsStoredPreference()
    {
        // Arrange
        _context.ProjectPreferences.Add(new ProjectPreference
        {
            ProjectId = "proj-stored",
            AgentName = "my-agent",
            DefaultModelId = "anthropic/claude-sonnet-4-5",
            ThinkingLevel = ThinkingLevel.High,
            AutoAccept = true,
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetOrDefaultAsync("proj-stored");

        // Assert
        result.Should().NotBeNull();
        result.ProjectId.Should().Be("proj-stored");
        result.AgentName.Should().Be("my-agent");
        result.DefaultModelId.Should().Be("anthropic/claude-sonnet-4-5");
        result.ThinkingLevel.Should().Be(ThinkingLevel.High);
        result.AutoAccept.Should().BeTrue();
    }

    [Fact]
    public async Task GetOrDefaultAsync_WhenNoPreference_ReturnsDefaultValues()
    {
        // Act
        var result = await _sut.GetOrDefaultAsync("proj-nonexistent");

        // Assert
        result.Should().NotBeNull();
        result.AgentName.Should().BeNull();
        result.ThinkingLevel.Should().Be(ThinkingLevel.Medium);
        result.AutoAccept.Should().BeFalse();
        result.DefaultModelId.Should().BeNull();
    }

    [Fact]
    public async Task GetOrDefaultAsync_WhenNoPreference_DoesNotInsertRow()
    {
        // Act
        await _sut.GetOrDefaultAsync("proj-no-insert");

        // Assert
        var count = await _context.ProjectPreferences
            .AsNoTracking()
            .CountAsync(p => p.ProjectId == "proj-no-insert");
        count.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetOrDefaultAsync_WhenProjectIdIsNullOrWhiteSpace_ThrowsArgumentException(string? projectId)
    {
        // Act
        var act = async () => await _sut.GetOrDefaultAsync(projectId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── SetAgentAsync — create new ──────────────────────────────────────────

    [Fact]
    public async Task SetAgentAsync_WhenNoPreferenceExists_CreatesNewPreference()
    {
        // Act
        var result = await _sut.SetAgentAsync("proj-agent-new", "my-agent");

        // Assert
        result.Should().BeTrue();
        var saved = await _context.ProjectPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == "proj-agent-new");
        saved.Should().NotBeNull();
        saved!.AgentName.Should().Be("my-agent");
    }

    // ─── SetAgentAsync — update existing ─────────────────────────────────────

    [Fact]
    public async Task SetAgentAsync_WhenPreferenceExists_UpdatesAgentName()
    {
        // Arrange
        _context.ProjectPreferences.Add(new ProjectPreference
        {
            ProjectId = "proj-agent-update",
            AgentName = "old-agent",
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.SetAgentAsync("proj-agent-update", "new-agent");

        // Assert
        result.Should().BeTrue();
        _context.ChangeTracker.Clear();
        var updated = await _context.ProjectPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == "proj-agent-update");
        updated.Should().NotBeNull();
        updated!.AgentName.Should().Be("new-agent");
    }

    [Fact]
    public async Task SetAgentAsync_WhenAgentNameIsNull_SetsNullAgentName()
    {
        // Arrange
        _context.ProjectPreferences.Add(new ProjectPreference
        {
            ProjectId = "proj-agent-null",
            AgentName = "some-agent",
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.SetAgentAsync("proj-agent-null", null);

        // Assert
        result.Should().BeTrue();
        _context.ChangeTracker.Clear();
        var updated = await _context.ProjectPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == "proj-agent-null");
        updated.Should().NotBeNull();
        updated!.AgentName.Should().BeNull();
    }

    // ─── SetAgentAsync — argument validation ─────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetAgentAsync_WhenProjectIdIsNullOrWhiteSpace_ThrowsArgumentException(string? projectId)
    {
        // Act
        var act = async () => await _sut.SetAgentAsync(projectId!, "my-agent");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── SetThinkingLevelAsync — create new ──────────────────────────────────

    [Fact]
    public async Task SetThinkingLevelAsync_WhenNoPreferenceExists_CreatesNewPreference()
    {
        // Act
        var result = await _sut.SetThinkingLevelAsync("proj-thinking-new", ThinkingLevel.High);

        // Assert
        result.Should().BeTrue();
        var saved = await _context.ProjectPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == "proj-thinking-new");
        saved.Should().NotBeNull();
        saved!.ThinkingLevel.Should().Be(ThinkingLevel.High);
    }

    // ─── SetThinkingLevelAsync — update existing ──────────────────────────────

    [Fact]
    public async Task SetThinkingLevelAsync_WhenPreferenceExists_UpdatesThinkingLevel()
    {
        // Arrange
        _context.ProjectPreferences.Add(new ProjectPreference
        {
            ProjectId = "proj-thinking-update",
            ThinkingLevel = ThinkingLevel.Low,
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.SetThinkingLevelAsync("proj-thinking-update", ThinkingLevel.High);

        // Assert
        result.Should().BeTrue();
        _context.ChangeTracker.Clear();
        var updated = await _context.ProjectPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == "proj-thinking-update");
        updated.Should().NotBeNull();
        updated!.ThinkingLevel.Should().Be(ThinkingLevel.High);
    }

    // ─── SetThinkingLevelAsync — argument validation ──────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetThinkingLevelAsync_WhenProjectIdIsNullOrWhiteSpace_ThrowsArgumentException(string? projectId)
    {
        // Act
        var act = async () => await _sut.SetThinkingLevelAsync(projectId!, ThinkingLevel.Medium);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── ClearDefaultModelAsync — update existing ────────────────────────────

    [Fact]
    public async Task ClearDefaultModelAsync_WhenPreferenceExists_SetsDefaultModelIdToNull()
    {
        // Arrange
        _context.ProjectPreferences.Add(new ProjectPreference
        {
            ProjectId = "proj-clear-model",
            DefaultModelId = "anthropic/claude-sonnet-4-5",
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.ClearDefaultModelAsync("proj-clear-model");

        // Assert
        result.Should().BeTrue();
        _context.ChangeTracker.Clear();
        var updated = await _context.ProjectPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == "proj-clear-model");
        updated.Should().NotBeNull();
        updated!.DefaultModelId.Should().BeNull();
    }

    // ─── ClearDefaultModelAsync — create new ─────────────────────────────────

    [Fact]
    public async Task ClearDefaultModelAsync_WhenNoPreferenceExists_CreatesNewPreferenceWithNullModel()
    {
        // Act
        var result = await _sut.ClearDefaultModelAsync("proj-clear-new");

        // Assert
        result.Should().BeTrue();
        var saved = await _context.ProjectPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == "proj-clear-new");
        saved.Should().NotBeNull();
        saved!.DefaultModelId.Should().BeNull();
    }

    // ─── ClearDefaultModelAsync — argument validation ─────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ClearDefaultModelAsync_WhenProjectIdIsNullOrWhiteSpace_ThrowsArgumentException(string? projectId)
    {
        // Act
        var act = async () => await _sut.ClearDefaultModelAsync(projectId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── SetAutoAcceptAsync — create new ─────────────────────────────────────

    [Fact]
    public async Task SetAutoAcceptAsync_WhenNoPreferenceExists_CreatesNewPreference()
    {
        // Act
        var result = await _sut.SetAutoAcceptAsync("proj-autoacc-new", true);

        // Assert
        result.Should().BeTrue();
        var saved = await _context.ProjectPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == "proj-autoacc-new");
        saved.Should().NotBeNull();
        saved!.AutoAccept.Should().BeTrue();
    }

    // ─── SetAutoAcceptAsync — update existing ─────────────────────────────────

    [Fact]
    public async Task SetAutoAcceptAsync_WhenPreferenceExists_UpdatesAutoAccept()
    {
        // Arrange
        _context.ProjectPreferences.Add(new ProjectPreference
        {
            ProjectId = "proj-autoacc-update",
            AutoAccept = false,
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.SetAutoAcceptAsync("proj-autoacc-update", true);

        // Assert
        result.Should().BeTrue();
        _context.ChangeTracker.Clear();
        var updated = await _context.ProjectPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectId == "proj-autoacc-update");
        updated.Should().NotBeNull();
        updated!.AutoAccept.Should().BeTrue();
    }

    // ─── SetAutoAcceptAsync — argument validation ─────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SetAutoAcceptAsync_WhenProjectIdIsNullOrWhiteSpace_ThrowsArgumentException(string? projectId)
    {
        // Act
        var act = async () => await _sut.SetAutoAcceptAsync(projectId!, true);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── Error-path: DB failure returns false ─────────────────────────────────

    [Fact]
    public async Task SetAgentAsync_WhenDbFails_ReturnsFalse()
    {
        // Arrange
        _connection.Close(); // force DB failure

        // Act
        var result = await _sut.SetAgentAsync("proj-1", "my-agent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetThinkingLevelAsync_WhenDbFails_ReturnsFalse()
    {
        // Arrange
        _connection.Close(); // force DB failure

        // Act
        var result = await _sut.SetThinkingLevelAsync("proj-1", ThinkingLevel.High);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetAutoAcceptAsync_WhenDbFails_ReturnsFalse()
    {
        // Arrange
        _connection.Close(); // force DB failure

        // Act
        var result = await _sut.SetAutoAcceptAsync("proj-1", true);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ClearDefaultModelAsync_WhenDbFails_ReturnsFalse()
    {
        // Arrange
        _connection.Close(); // force DB failure

        // Act
        var result = await _sut.ClearDefaultModelAsync("proj-1");

        // Assert
        result.Should().BeFalse();
    }
}
