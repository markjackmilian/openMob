using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;
using openMob.Core.Services;

namespace openMob.Tests.Services;

/// <summary>
/// Unit tests for <see cref="FileService"/>.
/// Covers GetFilesAsync (flat list), GetFileTreeAsync (tree navigation),
/// FindFilesAsync (server-side search), error propagation, file name extraction,
/// and cancellation.
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

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static OpencodeResult<IReadOnlyList<string>> SuccessPaths(params string[] paths)
        => OpencodeResult<IReadOnlyList<string>>.Success(paths.ToList().AsReadOnly());

    private static OpencodeResult<IReadOnlyList<FileNodeDto>> SuccessNodes(params FileNodeDto[] nodes)
        => OpencodeResult<IReadOnlyList<FileNodeDto>>.Success(nodes.ToList().AsReadOnly());

    private static FileNodeDto BuildNode(string name, string path, string type)
        => new(Name: name, Path: path, Absolute: $"/abs/{path}", Type: type, Ignored: false);

    private static OpencodeApiError ServerError(string message = "Internal server error")
        => new(ErrorKind.ServerError, message, 500, null);

    // ═════════════════════════════════════════════════════════════════════════
    // GetFilesAsync
    // ═════════════════════════════════════════════════════════════════════════

    // ─── GetFilesAsync — Happy Path ──────────────────────────────────────────

    [Fact]
    public async Task GetFilesAsync_WhenApiReturnsFiles_ReturnsMappedFileDtos()
    {
        // Arrange
        _apiClient
            .FindFilesAsync(Arg.Any<FindFilesRequest>(), Arg.Any<CancellationToken>())
            .Returns(SuccessPaths("src/foo/bar.cs", "README.md", "tests/unit/test.cs"));

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
        _apiClient
            .FindFilesAsync(Arg.Any<FindFilesRequest>(), Arg.Any<CancellationToken>())
            .Returns(SuccessPaths("src/foo/bar.cs"));

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
        _apiClient
            .FindFilesAsync(Arg.Any<FindFilesRequest>(), Arg.Any<CancellationToken>())
            .Returns(SuccessPaths(relativePath));

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
            .Returns(SuccessPaths());

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
        _apiClient
            .FindFilesAsync(Arg.Any<FindFilesRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<string>>.Failure(ServerError()));

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
            .Returns(SuccessPaths());

        // Act
        await _sut.GetFilesAsync();

        // Assert
        await _apiClient.Received(1).FindFilesAsync(
            Arg.Is<FindFilesRequest>(r => r.Pattern == "**" && r.Path == ""),
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
            .Returns(SuccessPaths());

        // Act
        await _sut.GetFilesAsync(cts.Token);

        // Assert
        await _apiClient.Received(1).FindFilesAsync(
            Arg.Any<FindFilesRequest>(),
            Arg.Any<CancellationToken>());
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetFileTreeAsync
    // ═════════════════════════════════════════════════════════════════════════

    // ─── GetFileTreeAsync — Happy Path ───────────────────────────────────────

    [Fact]
    public async Task GetFileTreeAsync_WhenApiReturnsNodes_ReturnsMappedFileDtos()
    {
        // Arrange
        var nodes = new[]
        {
            BuildNode("foo.cs", "src/foo.cs", "file"),
            BuildNode("Models", "src/Models", "directory"),
        };
        _apiClient
            .GetFileTreeAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(nodes));

        // Act
        var result = await _sut.GetFileTreeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetFileTreeAsync_MapsPathToRelativePath()
    {
        // Arrange
        _apiClient
            .GetFileTreeAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(BuildNode("foo.cs", "src/foo.cs", "file")));

        // Act
        var result = await _sut.GetFileTreeAsync();

        // Assert
        result.Value![0].RelativePath.Should().Be("src/foo.cs");
    }

    [Fact]
    public async Task GetFileTreeAsync_MapsNameCorrectly()
    {
        // Arrange
        _apiClient
            .GetFileTreeAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(BuildNode("foo.cs", "src/foo.cs", "file")));

        // Act
        var result = await _sut.GetFileTreeAsync();

        // Assert
        result.Value![0].Name.Should().Be("foo.cs");
    }

    [Theory]
    [InlineData("file")]
    [InlineData("directory")]
    public async Task GetFileTreeAsync_MapsTypeCorrectly(string type)
    {
        // Arrange
        _apiClient
            .GetFileTreeAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(BuildNode("item", "src/item", type)));

        // Act
        var result = await _sut.GetFileTreeAsync();

        // Assert
        result.Value![0].Type.Should().Be(type);
    }

    // ─── GetFileTreeAsync — With Path ────────────────────────────────────────

    [Fact]
    public async Task GetFileTreeAsync_WhenPathProvided_PassesPathToApiClient()
    {
        // Arrange
        _apiClient
            .GetFileTreeAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(SuccessNodes());

        // Act
        await _sut.GetFileTreeAsync("src/Models");

        // Assert
        await _apiClient.Received(1).GetFileTreeAsync(
            Arg.Is<string?>(p => p == "src/Models"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetFileTreeAsync_WhenPathIsNull_PassesNullToApiClient()
    {
        // Arrange
        // FileService passes path as-is (null for root). OpencodeApiClient normalises null
        // to an empty string internally when building the /file?pattern=*&path= query parameter.
        _apiClient
            .GetFileTreeAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(SuccessNodes());

        // Act
        await _sut.GetFileTreeAsync(null);

        // Assert
        await _apiClient.Received(1).GetFileTreeAsync(
            Arg.Is<string?>(p => p == null),
            Arg.Any<CancellationToken>());
    }

    // ─── GetFileTreeAsync — Empty / Error ────────────────────────────────────

    [Fact]
    public async Task GetFileTreeAsync_WhenApiReturnsEmpty_ReturnsEmptyList()
    {
        // Arrange
        _apiClient
            .GetFileTreeAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(SuccessNodes());

        // Act
        var result = await _sut.GetFileTreeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFileTreeAsync_WhenApiReturnsError_PropagatesError()
    {
        // Arrange
        _apiClient
            .GetFileTreeAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<FileNodeDto>>.Failure(ServerError()));

        // Act
        var result = await _sut.GetFileTreeAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.ServerError);
    }

    // ─── GetFileTreeAsync — Cancellation ─────────────────────────────────────

    [Fact]
    public async Task GetFileTreeAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _sut.GetFileTreeAsync(null, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // FindFilesAsync
    // ═════════════════════════════════════════════════════════════════════════

    // ─── FindFilesAsync — Happy Path ─────────────────────────────────────────

    [Fact]
    public async Task FindFilesAsync_WhenApiReturnsFiles_ReturnsMappedFileDtos()
    {
        // Arrange
        _apiClient
            .FindFilesAsync(Arg.Any<FindFilesRequest>(), Arg.Any<CancellationToken>())
            .Returns(SuccessPaths("src/foo.cs", "tests/bar.cs"));

        // Act
        var result = await _sut.FindFilesAsync("*foo*");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("src/foo/bar.cs", "bar.cs")]
    [InlineData("README.md", "README.md")]
    [InlineData("deeply/nested/path/to/file.txt", "file.txt")]
    [InlineData("single", "single")]
    public async Task FindFilesAsync_ExtractsFileNameCorrectly(string relativePath, string expectedName)
    {
        // Arrange
        _apiClient
            .FindFilesAsync(Arg.Any<FindFilesRequest>(), Arg.Any<CancellationToken>())
            .Returns(SuccessPaths(relativePath));

        // Act
        var result = await _sut.FindFilesAsync("*");

        // Assert
        result.Value![0].Name.Should().Be(expectedName);
    }

    [Fact]
    public async Task FindFilesAsync_PassesPatternToApiClient()
    {
        // Arrange
        _apiClient
            .FindFilesAsync(Arg.Any<FindFilesRequest>(), Arg.Any<CancellationToken>())
            .Returns(SuccessPaths());

        // Act
        await _sut.FindFilesAsync("*foo*");

        // Assert
        await _apiClient.Received(1).FindFilesAsync(
            Arg.Is<FindFilesRequest>(r => r.Pattern == "*foo*" && r.Path == ""),
            Arg.Any<CancellationToken>());
    }

    // ─── FindFilesAsync — Empty / Error ──────────────────────────────────────

    [Fact]
    public async Task FindFilesAsync_WhenApiReturnsEmpty_ReturnsEmptyList()
    {
        // Arrange
        _apiClient
            .FindFilesAsync(Arg.Any<FindFilesRequest>(), Arg.Any<CancellationToken>())
            .Returns(SuccessPaths());

        // Act
        var result = await _sut.FindFilesAsync("*nothing*");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task FindFilesAsync_WhenApiReturnsError_PropagatesError()
    {
        // Arrange
        _apiClient
            .FindFilesAsync(Arg.Any<FindFilesRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<string>>.Failure(ServerError("Search failed")));

        // Act
        var result = await _sut.FindFilesAsync("*foo*");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Be("Search failed");
    }

    // ─── FindFilesAsync — Cancellation ───────────────────────────────────────

    [Fact]
    public async Task FindFilesAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await _sut.FindFilesAsync("*foo*", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
