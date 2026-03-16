using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
}
