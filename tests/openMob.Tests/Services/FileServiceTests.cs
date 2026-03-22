using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;
using openMob.Core.Services;

namespace openMob.Tests.Services;

/// <summary>
/// Unit tests for <see cref="FileService"/>.
/// Covers happy path, empty results, error propagation, file name extraction, and cancellation.
/// </summary>
public sealed class FileServiceTests
{
    private readonly IOpencodeApiClient _apiClient;
    private readonly IFileService _sut;

    public FileServiceTests()
    {
        _apiClient = Substitute.For<IOpencodeApiClient>();
        _sut = new FileService(_apiClient);
    }

    // ─── GetFilesAsync — Happy Path ──────────────────────────────────────────

    [Fact]
    public async Task GetFilesAsync_WhenApiReturnsFiles_ReturnsMappedFileDtos()
    {
        // Arrange
        var paths = new List<string> { "src/foo/bar.cs", "README.md", "tests/unit/test.cs" };
        _apiClient
            .FindFilesAsync(Arg.Any<FindFilesRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<string>>.Success(paths));

        // Act
        var result = await _sut.GetFilesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetFilesAsync_WhenApiReturnsFiles_MapsRelativePathCorrectly()
    {
        // Arrange
        var paths = new List<string> { "src/foo/bar.cs" };
        _apiClient
            .FindFilesAsync(Arg.Any<FindFilesRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<string>>.Success(paths));

        // Act
        var result = await _sut.GetFilesAsync();

        // Assert
        result.Value![0].RelativePath.Should().Be("src/foo/bar.cs");
    }

    [Theory]
    [InlineData("src/foo/bar.cs", "bar.cs")]
    [InlineData("README.md", "README.md")]
    [InlineData("deeply/nested/path/to/file.txt", "file.txt")]
    public async Task GetFilesAsync_ExtractsFileNameCorrectly(string relativePath, string expectedName)
    {
        // Arrange
        var paths = new List<string> { relativePath };
        _apiClient
            .FindFilesAsync(Arg.Any<FindFilesRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<string>>.Success(paths));

        // Act
        var result = await _sut.GetFilesAsync();

        // Assert
        result.Value![0].Name.Should().Be(expectedName);
    }

    // ─── GetFilesAsync — Empty Result ────────────────────────────────────────

    [Fact]
    public async Task GetFilesAsync_WhenApiReturnsEmptyList_ReturnsEmptyList()
    {
        // Arrange
        _apiClient
            .FindFilesAsync(Arg.Any<FindFilesRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<string>>.Success(new List<string>()));

        // Act
        var result = await _sut.GetFilesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ─── GetFilesAsync — Error Propagation ───────────────────────────────────

    [Fact]
    public async Task GetFilesAsync_WhenApiReturnsFailure_PropagatesError()
    {
        // Arrange
        var error = new OpencodeApiError(ErrorKind.ServerError, "Internal server error", 500, null);
        _apiClient
            .FindFilesAsync(Arg.Any<FindFilesRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<string>>.Failure(error));

        // Act
        var result = await _sut.GetFilesAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.ServerError);
    }

    // ─── GetFilesAsync — Request Construction ────────────────────────────────

    [Fact]
    public async Task GetFilesAsync_CallsFindFilesAsyncWithWildcardPattern()
    {
        // Arrange
        _apiClient
            .FindFilesAsync(Arg.Any<FindFilesRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<string>>.Success(new List<string>()));

        // Act
        await _sut.GetFilesAsync();

        // Assert
        await _apiClient.Received(1).FindFilesAsync(
            Arg.Is<FindFilesRequest>(r => r.Pattern == "**" && r.Path == null),
            Arg.Any<CancellationToken>());
    }

    // ─── GetFilesAsync — Cancellation ────────────────────────────────────────

    [Fact]
    public async Task GetFilesAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _sut.GetFilesAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetFilesAsync_PassesCancellationTokenToApiClient()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        _apiClient
            .FindFilesAsync(Arg.Any<FindFilesRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<string>>.Success(new List<string>()));

        // Act
        await _sut.GetFilesAsync(cts.Token);

        // Assert
        await _apiClient.Received(1).FindFilesAsync(
            Arg.Any<FindFilesRequest>(),
            Arg.Any<CancellationToken>());
    }
}
