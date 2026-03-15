using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;
using openMob.Core.Services;

namespace openMob.Tests.Services;

/// <summary>
/// Unit tests for <see cref="SessionService"/>.
/// </summary>
public sealed class SessionServiceTests
{
    private readonly IOpencodeApiClient _apiClient;
    private readonly SessionService _sut;

    public SessionServiceTests()
    {
        _apiClient = Substitute.For<IOpencodeApiClient>();
        _sut = new SessionService(_apiClient);
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private static openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto BuildSession(
        string id = "sess-1",
        string projectId = "proj-1",
        string title = "Test Session",
        long updated = 1710000001000)
    {
        var time = new SessionTimeDto(Created: 1710000000000, Updated: updated, Compacting: null);
        return new openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto(
            Id: id, ProjectId: projectId, Directory: "/path", ParentId: null,
            Summary: null, Share: null, Title: title, Version: "1",
            Time: time, Revert: null);
    }

    // ─── GetAllSessionsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAllSessionsAsync_WhenApiReturnsSuccess_ReturnsSessionList()
    {
        // Arrange
        var sessions = new List<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>
        {
            BuildSession("s1"),
            BuildSession("s2"),
        };
        _apiClient.GetSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>>.Success(sessions));

        // Act
        var result = await _sut.GetAllSessionsAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllSessionsAsync_WhenApiReturnsFailure_ReturnsEmptyList()
    {
        // Arrange
        _apiClient.GetSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>>.Failure(
                new OpencodeApiError(ErrorKind.ServerError, "Server error", 500, null)));

        // Act
        var result = await _sut.GetAllSessionsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    // ─── GetSessionsByProjectAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetSessionsByProjectAsync_FiltersAndOrdersByUpdatedDescending()
    {
        // Arrange
        var sessions = new List<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>
        {
            BuildSession("s1", "proj-1", "Oldest", updated: 1000),
            BuildSession("s2", "proj-2", "Other project", updated: 3000),
            BuildSession("s3", "proj-1", "Newest", updated: 2000),
        };
        _apiClient.GetSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>>.Success(sessions));

        // Act
        var result = await _sut.GetSessionsByProjectAsync("proj-1");

        // Assert
        result.Should().HaveCount(2);
        result[0].Id.Should().Be("s3"); // Newest first (updated: 2000)
        result[1].Id.Should().Be("s1"); // Oldest second (updated: 1000)
    }

    [Fact]
    public async Task GetSessionsByProjectAsync_WhenNoMatchingProject_ReturnsEmptyList()
    {
        // Arrange
        var sessions = new List<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>
        {
            BuildSession("s1", "proj-1"),
        };
        _apiClient.GetSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>>.Success(sessions));

        // Act
        var result = await _sut.GetSessionsByProjectAsync("proj-999");

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetSessionsByProjectAsync_WhenProjectIdIsNullOrWhitespace_ThrowsArgumentException(string? projectId)
    {
        // Act
        var act = async () => await _sut.GetSessionsByProjectAsync(projectId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── GetSessionAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetSessionAsync_WhenApiReturnsSuccess_ReturnsSession()
    {
        // Arrange
        var session = BuildSession("s1");
        _apiClient.GetSessionAsync("s1", Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>.Success(session));

        // Act
        var result = await _sut.GetSessionAsync("s1");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("s1");
    }

    [Fact]
    public async Task GetSessionAsync_WhenApiReturnsNotFound_ReturnsNull()
    {
        // Arrange
        _apiClient.GetSessionAsync("missing", Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>.Failure(
                new OpencodeApiError(ErrorKind.NotFound, "Not found", 404, null)));

        // Act
        var result = await _sut.GetSessionAsync("missing");

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetSessionAsync_WhenIdIsNullOrWhitespace_ThrowsArgumentException(string? id)
    {
        // Act
        var act = async () => await _sut.GetSessionAsync(id!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── CreateSessionAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateSessionAsync_WhenApiReturnsSuccess_ReturnsCreatedSession()
    {
        // Arrange
        var session = BuildSession("new-sess", title: "My Session");
        _apiClient.CreateSessionAsync(Arg.Any<CreateSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>.Success(session));

        // Act
        var result = await _sut.CreateSessionAsync("My Session");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("new-sess");
    }

    [Fact]
    public async Task CreateSessionAsync_PassesCorrectTitleToApi()
    {
        // Arrange
        var session = BuildSession();
        _apiClient.CreateSessionAsync(Arg.Any<CreateSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>.Success(session));

        // Act
        await _sut.CreateSessionAsync("Test Title");

        // Assert
        await _apiClient.Received(1).CreateSessionAsync(
            Arg.Is<CreateSessionRequest>(r => r.Title == "Test Title" && r.ParentId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSessionAsync_WhenApiReturnsFailure_ReturnsNull()
    {
        // Arrange
        _apiClient.CreateSessionAsync(Arg.Any<CreateSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>.Failure(
                new OpencodeApiError(ErrorKind.ServerError, "Error", 500, null)));

        // Act
        var result = await _sut.CreateSessionAsync("Test");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateSessionAsync_WhenTitleIsNull_PassesNullTitle()
    {
        // Arrange
        var session = BuildSession();
        _apiClient.CreateSessionAsync(Arg.Any<CreateSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>.Success(session));

        // Act
        await _sut.CreateSessionAsync(null);

        // Assert
        await _apiClient.Received(1).CreateSessionAsync(
            Arg.Is<CreateSessionRequest>(r => r.Title == null),
            Arg.Any<CancellationToken>());
    }

    // ─── UpdateSessionTitleAsync ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateSessionTitleAsync_WhenApiReturnsSuccess_ReturnsTrue()
    {
        // Arrange
        var session = BuildSession("s1", title: "New Title");
        _apiClient.UpdateSessionAsync("s1", Arg.Any<UpdateSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>.Success(session));

        // Act
        var result = await _sut.UpdateSessionTitleAsync("s1", "New Title");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateSessionTitleAsync_WhenApiReturnsFailure_ReturnsFalse()
    {
        // Arrange
        _apiClient.UpdateSessionAsync("s1", Arg.Any<UpdateSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>.Failure(
                new OpencodeApiError(ErrorKind.ServerError, "Error", 500, null)));

        // Act
        var result = await _sut.UpdateSessionTitleAsync("s1", "New Title");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateSessionTitleAsync_PassesCorrectTitleToApi()
    {
        // Arrange
        var session = BuildSession();
        _apiClient.UpdateSessionAsync("s1", Arg.Any<UpdateSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>.Success(session));

        // Act
        await _sut.UpdateSessionTitleAsync("s1", "Updated Title");

        // Assert
        await _apiClient.Received(1).UpdateSessionAsync(
            "s1",
            Arg.Is<UpdateSessionRequest>(r => r.Title == "Updated Title"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null, "title")]
    [InlineData("", "title")]
    [InlineData("id", null)]
    [InlineData("id", "")]
    public async Task UpdateSessionTitleAsync_WhenIdOrTitleIsNullOrWhitespace_ThrowsArgumentException(string? id, string? title)
    {
        // Act
        var act = async () => await _sut.UpdateSessionTitleAsync(id!, title!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── DeleteSessionAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteSessionAsync_WhenApiReturnsSuccess_ReturnsTrue()
    {
        // Arrange
        _apiClient.DeleteSessionAsync("s1", Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<bool>.Success(true));

        // Act
        var result = await _sut.DeleteSessionAsync("s1");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteSessionAsync_WhenApiReturnsFailure_ReturnsFalse()
    {
        // Arrange
        _apiClient.DeleteSessionAsync("s1", Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<bool>.Failure(
                new OpencodeApiError(ErrorKind.ServerError, "Error", 500, null)));

        // Act
        var result = await _sut.DeleteSessionAsync("s1");

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteSessionAsync_WhenIdIsNullOrWhitespace_ThrowsArgumentException(string? id)
    {
        // Act
        var act = async () => await _sut.DeleteSessionAsync(id!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── ForkSessionAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ForkSessionAsync_WhenApiReturnsSuccess_ReturnsForkedSession()
    {
        // Arrange
        var forked = BuildSession("forked-sess");
        _apiClient.ForkSessionAsync("s1", Arg.Any<ForkSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>.Success(forked));

        // Act
        var result = await _sut.ForkSessionAsync("s1");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("forked-sess");
    }

    [Fact]
    public async Task ForkSessionAsync_PassesNullMessageIdToApi()
    {
        // Arrange
        var forked = BuildSession();
        _apiClient.ForkSessionAsync("s1", Arg.Any<ForkSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>.Success(forked));

        // Act
        await _sut.ForkSessionAsync("s1");

        // Assert
        await _apiClient.Received(1).ForkSessionAsync(
            "s1",
            Arg.Is<ForkSessionRequest>(r => r.MessageId == null && r.Title == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ForkSessionAsync_WhenApiReturnsFailure_ReturnsNull()
    {
        // Arrange
        _apiClient.ForkSessionAsync("s1", Arg.Any<ForkSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>.Failure(
                new OpencodeApiError(ErrorKind.ServerError, "Error", 500, null)));

        // Act
        var result = await _sut.ForkSessionAsync("s1");

        // Assert
        result.Should().BeNull();
    }

    // ─── GetLastSessionForProjectAsync ────────────────────────────────────────

    [Fact]
    public async Task GetLastSessionForProjectAsync_WhenSessionsExist_ReturnsMostRecentlyUpdated()
    {
        // Arrange
        var sessions = new List<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>
        {
            BuildSession("s1", "proj-1", "Older", updated: 1000),
            BuildSession("s2", "proj-1", "Newer", updated: 2000),
        };
        _apiClient.GetSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>>.Success(sessions));

        // Act
        var result = await _sut.GetLastSessionForProjectAsync("proj-1");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("s2");
    }

    [Fact]
    public async Task GetLastSessionForProjectAsync_WhenNoSessionsForProject_ReturnsNull()
    {
        // Arrange
        var sessions = new List<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>
        {
            BuildSession("s1", "proj-other"),
        };
        _apiClient.GetSessionsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<openMob.Core.Infrastructure.Http.Dtos.Opencode.SessionDto>>.Success(sessions));

        // Act
        var result = await _sut.GetLastSessionForProjectAsync("proj-1");

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetLastSessionForProjectAsync_WhenProjectIdIsNullOrWhitespace_ThrowsArgumentException(string? projectId)
    {
        // Act
        var act = async () => await _sut.GetLastSessionForProjectAsync(projectId!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
