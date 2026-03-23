using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;
using openMob.Core.Services;

namespace openMob.Tests.Services;

/// <summary>
/// Unit tests for <see cref="FileService"/>.
/// Covers GetFilesAsync (root tree), GetFileTreeAsync (tree navigation),
/// FindFilesAsync (BFS traversal + client-side filter), error propagation,
/// ignored-node skipping, and cancellation.
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

    private static OpencodeResult<IReadOnlyList<FileNodeDto>> SuccessNodes(params FileNodeDto[] nodes)
        => OpencodeResult<IReadOnlyList<FileNodeDto>>.Success(nodes.ToList().AsReadOnly());

    private static FileNodeDto BuildFileNode(string name, string path)
        => new(Name: name, Path: path, Absolute: $"/abs/{path}", Type: "file", Ignored: false);

    private static FileNodeDto BuildDirNode(string name, string path, bool ignored = false)
        => new(Name: name, Path: path, Absolute: $"/abs/{path}", Type: "directory", Ignored: ignored);

    private static FileNodeDto BuildIgnoredFileNode(string name, string path)
        => new(Name: name, Path: path, Absolute: $"/abs/{path}", Type: "file", Ignored: true);

    private static OpencodeApiError ServerError(string message = "Internal server error")
        => new(ErrorKind.ServerError, message, 500, null);

    // ═════════════════════════════════════════════════════════════════════════
    // GetFilesAsync
    // ═════════════════════════════════════════════════════════════════════════

    // ─── GetFilesAsync — Happy Path ──────────────────────────────────────────

    [Fact]
    public async Task GetFilesAsync_WhenApiReturnsNodes_ReturnsMappedFileDtos()
    {
        // Arrange
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(
                BuildFileNode("foo.cs", "src/foo.cs"),
                BuildFileNode("README.md", "README.md"),
                BuildFileNode("test.cs", "tests/test.cs")));

        // Act
        var result = await _sut.GetFilesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetFilesAsync_WhenApiReturnsNodes_MapsRelativePathCorrectly()
    {
        // Arrange
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(BuildFileNode("foo.cs", "src/foo.cs")));

        // Act
        var result = await _sut.GetFilesAsync();

        // Assert
        result.Value![0].RelativePath.Should().Be("src/foo.cs");
    }

    [Fact]
    public async Task GetFilesAsync_WhenApiReturnsNodes_MapsNameCorrectly()
    {
        // Arrange
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(BuildFileNode("foo.cs", "src/foo.cs")));

        // Act
        var result = await _sut.GetFilesAsync();

        // Assert
        result.Value![0].Name.Should().Be("foo.cs");
    }

    [Fact]
    public async Task GetFilesAsync_WhenApiReturnsNodes_MapsTypeCorrectly()
    {
        // Arrange
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(BuildFileNode("foo.cs", "src/foo.cs")));

        // Act
        var result = await _sut.GetFilesAsync();

        // Assert
        result.Value![0].Type.Should().Be("file");
    }

    // ─── GetFilesAsync — Empty Result ────────────────────────────────────────

    [Fact]
    public async Task GetFilesAsync_WhenApiReturnsEmptyList_ReturnsEmptyList()
    {
        // Arrange
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes());

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
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<FileNodeDto>>.Failure(ServerError()));

        // Act
        var result = await _sut.GetFilesAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ErrorKind.ServerError);
    }

    // ─── GetFilesAsync — API Call Verification ───────────────────────────────

    [Fact]
    public async Task GetFilesAsync_CallsGetFileTreeAsyncWithNullPath()
    {
        // Arrange
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes());

        // Act
        await _sut.GetFilesAsync();

        // Assert
        await _apiClient.Received(1).GetFileTreeAsync(
            Arg.Is<string?>(p => p == null),
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
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes());

        // Act
        await _sut.GetFilesAsync(cts.Token);

        // Assert
        await _apiClient.Received(1).GetFileTreeAsync(
            Arg.Any<string?>(),
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
            BuildFileNode("foo.cs", "src/foo.cs"),
            BuildDirNode("Models", "src/Models"),
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
            .Returns(SuccessNodes(BuildFileNode("foo.cs", "src/foo.cs")));

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
            .Returns(SuccessNodes(BuildFileNode("foo.cs", "src/foo.cs")));

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
        var node = new FileNodeDto(Name: "item", Path: "src/item", Absolute: "/abs/src/item", Type: type, Ignored: false);
        _apiClient
            .GetFileTreeAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(node));

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
    public async Task FindFilesAsync_WhenRootContainsMatchingFiles_ReturnsFilteredResults()
    {
        // Arrange — root level has two files; only one matches "*ViewModel*"
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(
                BuildFileNode("SessionViewModel.cs", "ViewModels/SessionViewModel.cs"),
                BuildFileNode("SessionService.cs", "Services/SessionService.cs")));

        // Act
        var result = await _sut.FindFilesAsync("*ViewModel*");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Name.Should().Be("SessionViewModel.cs");
    }

    [Fact]
    public async Task FindFilesAsync_WhenMatchingFilesAreInSubdirectory_ReturnsThemViaRecursion()
    {
        // Arrange — root has a directory; the matching file is one level deep.
        // The "ViewModels" directory itself also matches "*ViewModel*" and is included in results.
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(
                BuildDirNode("ViewModels", "ViewModels")));

        _apiClient
            .GetFileTreeAsync("ViewModels", Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(
                BuildFileNode("SessionViewModel.cs", "ViewModels/SessionViewModel.cs")));

        // Act
        var result = await _sut.FindFilesAsync("*ViewModel*");

        // Assert — both the directory "ViewModels" and the file "SessionViewModel.cs" match
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(f => f.RelativePath == "ViewModels" && f.Type == "directory");
        result.Value.Should().Contain(f => f.RelativePath == "ViewModels/SessionViewModel.cs" && f.Type == "file");
    }

    [Fact]
    public async Task FindFilesAsync_WhenMatchingFilesAreAtMultipleLevels_ReturnsAllMatches()
    {
        // Arrange — matching nodes exist at root and one level deep.
        // "BaseViewModel.cs" (file), "ViewModels" (directory), and "SessionViewModel.cs" (file)
        // all contain "ViewModel" in their name, so all three are returned.
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(
                BuildFileNode("BaseViewModel.cs", "BaseViewModel.cs"),
                BuildDirNode("ViewModels", "ViewModels")));

        _apiClient
            .GetFileTreeAsync("ViewModels", Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(
                BuildFileNode("SessionViewModel.cs", "ViewModels/SessionViewModel.cs")));

        // Act
        var result = await _sut.FindFilesAsync("*ViewModel*");

        // Assert — all three nodes match: the file at root, the directory, and the file inside it
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value.Should().Contain(f => f.Name == "BaseViewModel.cs");
        result.Value.Should().Contain(f => f.Name == "ViewModels" && f.Type == "directory");
        result.Value.Should().Contain(f => f.Name == "SessionViewModel.cs");
    }

    [Fact]
    public async Task FindFilesAsync_MapsRelativePathCorrectly()
    {
        // Arrange
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(
                BuildFileNode("SessionViewModel.cs", "ViewModels/SessionViewModel.cs")));

        // Act
        var result = await _sut.FindFilesAsync("*ViewModel*");

        // Assert
        result.Value![0].RelativePath.Should().Be("ViewModels/SessionViewModel.cs");
    }

    [Fact]
    public async Task FindFilesAsync_MapsNameCorrectly()
    {
        // Arrange
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(
                BuildFileNode("SessionViewModel.cs", "ViewModels/SessionViewModel.cs")));

        // Act
        var result = await _sut.FindFilesAsync("*ViewModel*");

        // Assert
        result.Value![0].Name.Should().Be("SessionViewModel.cs");
    }

    // ─── FindFilesAsync — Case-Insensitive Matching ───────────────────────────

    [Theory]
    [InlineData("*viewmodel*")]
    [InlineData("*VIEWMODEL*")]
    [InlineData("*ViewModel*")]
    [InlineData("*viewModel*")]
    public async Task FindFilesAsync_MatchesFileNameCaseInsensitively(string pattern)
    {
        // Arrange
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(
                BuildFileNode("SessionViewModel.cs", "ViewModels/SessionViewModel.cs")));

        // Act
        var result = await _sut.FindFilesAsync(pattern);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    // ─── FindFilesAsync — Wildcard-Only Pattern ───────────────────────────────

    [Theory]
    [InlineData("*")]
    [InlineData("**")]
    [InlineData("")]
    public async Task FindFilesAsync_WhenPatternIsWildcardOnly_ReturnsAllNodes(string pattern)
    {
        // Arrange — two files at root, no subdirectories
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(
                BuildFileNode("foo.cs", "foo.cs"),
                BuildFileNode("bar.cs", "bar.cs")));

        // Act
        var result = await _sut.FindFilesAsync(pattern);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    // ─── FindFilesAsync — Ignored Nodes ──────────────────────────────────────

    [Fact]
    public async Task FindFilesAsync_SkipsIgnoredFiles()
    {
        // Arrange — one normal file, one ignored file; both match the pattern
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(
                BuildFileNode("foo.cs", "foo.cs"),
                BuildIgnoredFileNode("foo.ignored.cs", "foo.ignored.cs")));

        // Act
        var result = await _sut.FindFilesAsync("*foo*");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Name.Should().Be("foo.cs");
    }

    [Fact]
    public async Task FindFilesAsync_SkipsIgnoredDirectoriesAndDoesNotTraverseThem()
    {
        // Arrange — root has one normal dir and one ignored dir
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(
                BuildDirNode("src", "src"),
                BuildDirNode("node_modules", "node_modules", ignored: true)));

        _apiClient
            .GetFileTreeAsync("src", Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(BuildFileNode("foo.cs", "src/foo.cs")));

        // Act
        var result = await _sut.FindFilesAsync("*foo*");

        // Assert — node_modules should never be traversed
        await _apiClient.DidNotReceive().GetFileTreeAsync(
            Arg.Is<string?>(p => p == "node_modules"),
            Arg.Any<CancellationToken>());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    // ─── FindFilesAsync — Empty Result ───────────────────────────────────────

    [Fact]
    public async Task FindFilesAsync_WhenNoFilesMatch_ReturnsEmptyList()
    {
        // Arrange
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(
                BuildFileNode("foo.cs", "foo.cs"),
                BuildFileNode("bar.cs", "bar.cs")));

        // Act
        var result = await _sut.FindFilesAsync("*nothing*");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task FindFilesAsync_WhenRootIsEmpty_ReturnsEmptyList()
    {
        // Arrange
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes());

        // Act
        var result = await _sut.FindFilesAsync("*foo*");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    // ─── FindFilesAsync — Error Propagation ──────────────────────────────────

    [Fact]
    public async Task FindFilesAsync_WhenRootApiCallFails_PropagatesError()
    {
        // Arrange
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<FileNodeDto>>.Failure(ServerError("Search failed")));

        // Act
        var result = await _sut.FindFilesAsync("*foo*");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Message.Should().Be("Search failed");
    }

    [Fact]
    public async Task FindFilesAsync_WhenSubdirectoryApiCallFails_PropagatesError()
    {
        // Arrange — root succeeds but the subdirectory call fails
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(BuildDirNode("src", "src")));

        _apiClient
            .GetFileTreeAsync("src", Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<FileNodeDto>>.Failure(ServerError("Subdirectory error")));

        // Act
        var result = await _sut.FindFilesAsync("*foo*");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Be("Subdirectory error");
    }

    // ─── FindFilesAsync — BFS Traversal ──────────────────────────────────────

    [Fact]
    public async Task FindFilesAsync_TraversesDirectoriesInBreadthFirstOrder()
    {
        // Arrange — root → src → ViewModels (3 levels).
        // "ViewModels" (directory) and "SessionViewModel.cs" (file) both match "*ViewModel*".
        // "src" does not match.
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(BuildDirNode("src", "src")));

        _apiClient
            .GetFileTreeAsync("src", Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(BuildDirNode("ViewModels", "src/ViewModels")));

        _apiClient
            .GetFileTreeAsync("src/ViewModels", Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(BuildFileNode("SessionViewModel.cs", "src/ViewModels/SessionViewModel.cs")));

        // Act
        var result = await _sut.FindFilesAsync("*ViewModel*");

        // Assert — both the "ViewModels" directory and the file inside it match the pattern
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(f => f.RelativePath == "src/ViewModels" && f.Type == "directory");
        result.Value.Should().Contain(f => f.RelativePath == "src/ViewModels/SessionViewModel.cs" && f.Type == "file");
    }

    [Fact]
    public async Task FindFilesAsync_DoesNotTraverseDirectoriesBeyondMaxDepth()
    {
        // Arrange — build a chain of 5 nested directories (depth 0..4)
        // The 5th level (depth=4) should NOT be traversed because MaxSearchDepth = 4.
        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(BuildDirNode("d1", "d1")));

        _apiClient
            .GetFileTreeAsync("d1", Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(BuildDirNode("d2", "d1/d2")));

        _apiClient
            .GetFileTreeAsync("d1/d2", Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(BuildDirNode("d3", "d1/d2/d3")));

        _apiClient
            .GetFileTreeAsync("d1/d2/d3", Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(BuildDirNode("d4", "d1/d2/d3/d4")));

        _apiClient
            .GetFileTreeAsync("d1/d2/d3/d4", Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(BuildFileNode("deep.cs", "d1/d2/d3/d4/deep.cs")));

        // Act
        var result = await _sut.FindFilesAsync("*deep*");

        // Assert — d1/d2/d3/d4 is at depth 4 which equals MaxSearchDepth,
        // so it IS traversed (depth < MaxSearchDepth is checked before enqueuing children,
        // meaning depth 4 nodes are visited but their children are not enqueued).
        // The file at depth 4 should be found.
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Name.Should().Be("deep.cs");

        // The directory at depth 4 should NOT have its children enqueued (no call for d5).
        await _apiClient.DidNotReceive().GetFileTreeAsync(
            Arg.Is<string?>(p => p != null && p.Contains("d5")),
            Arg.Any<CancellationToken>());
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

    [Fact]
    public async Task FindFilesAsync_PassesCancellationTokenToEveryApiCall()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        _apiClient
            .GetFileTreeAsync(null, Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(BuildDirNode("src", "src")));

        _apiClient
            .GetFileTreeAsync("src", Arg.Any<CancellationToken>())
            .Returns(SuccessNodes(BuildFileNode("foo.cs", "src/foo.cs")));

        // Act
        await _sut.FindFilesAsync("*foo*", cts.Token);

        // Assert — both the root call and the subdirectory call received the token
        await _apiClient.Received(1).GetFileTreeAsync(null, cts.Token);
        await _apiClient.Received(1).GetFileTreeAsync("src", cts.Token);
    }
}
