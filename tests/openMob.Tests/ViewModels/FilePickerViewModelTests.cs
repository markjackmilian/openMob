using openMob.Core.Infrastructure.Http;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="FilePickerViewModel"/>.
/// Covers tree navigation, server-side search with debounce, file selection callback,
/// error handling, loading state, empty state, and back stack management.
/// </summary>
public sealed class FilePickerViewModelTests
{
    private readonly IFileService _fileService;
    private readonly IAppPopupService _popupService;
    private readonly FilePickerViewModel _sut;

    public FilePickerViewModelTests()
    {
        _fileService = Substitute.For<IFileService>();
        _popupService = Substitute.For<IAppPopupService>();
        _sut = new FilePickerViewModel(_fileService, _popupService);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static OpencodeResult<IReadOnlyList<FileDto>> SuccessFiles(params FileDto[] files)
        => OpencodeResult<IReadOnlyList<FileDto>>.Success(files.ToList().AsReadOnly());

    private static OpencodeResult<IReadOnlyList<FileDto>> FailureResult(string message = "Server error")
        => OpencodeResult<IReadOnlyList<FileDto>>.Failure(
            new OpencodeApiError(ErrorKind.ServerError, message, 500, null));

    private static OpencodeResult<IReadOnlyList<FileDto>> FailureResultNullMessage()
        => OpencodeResult<IReadOnlyList<FileDto>>.Failure(
            new OpencodeApiError(ErrorKind.ServerError, null!, 500, null));

    private static FileDto BuildFile(string relativePath, string name, string type = "file")
        => new(relativePath, name, type);

    private static FileDto BuildDirectory(string relativePath, string name)
        => new(relativePath, name, "directory");

    /// <summary>
    /// Configures <see cref="IFileService.GetFileTreeAsync"/> to return the given files
    /// for any path and cancellation token.
    /// </summary>
    private void SetupTreeReturn(params FileDto[] files)
    {
        _fileService
            .GetFileTreeAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(SuccessFiles(files));
    }

    /// <summary>
    /// Configures <see cref="IFileService.FindFilesAsync"/> to return the given files
    /// for any pattern and cancellation token.
    /// </summary>
    private void SetupSearchReturn(params FileDto[] files)
    {
        _fileService
            .FindFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SuccessFiles(files));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Constructor
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Assert
        _sut.Items.Should().BeEmpty();
        _sut.SearchText.Should().BeEmpty();
        _sut.IsLoading.Should().BeFalse();
        _sut.IsEmpty.Should().BeFalse();
        _sut.HasError.Should().BeFalse();
        _sut.ErrorMessage.Should().BeNull();
        _sut.CurrentPath.Should().BeNull();
        _sut.IsSearchActive.Should().BeFalse();
        _sut.CanGoBack.Should().BeFalse();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // LoadFilesCommand — Happy Path
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoadFilesCommand_WhenServiceReturnsNodes_PopulatesItems()
    {
        // Arrange
        SetupTreeReturn(
            BuildFile("src/foo.cs", "foo.cs"),
            BuildFile("README.md", "README.md"));

        // Act
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Assert
        _sut.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadFilesCommand_WhenServiceReturnsNodes_MapsCorrectly()
    {
        // Arrange
        SetupTreeReturn(BuildFile("src/foo.cs", "foo.cs"));

        // Act
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Assert
        _sut.Items[0].RelativePath.Should().Be("src/foo.cs");
        _sut.Items[0].Name.Should().Be("foo.cs");
    }

    [Fact]
    public async Task LoadFilesCommand_WhenServiceReturnsEmpty_SetsIsEmpty()
    {
        // Arrange
        SetupTreeReturn();

        // Act
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Assert
        _sut.IsEmpty.Should().BeTrue();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // LoadFilesCommand — Error Path
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoadFilesCommand_WhenServiceReturnsError_SetsHasError()
    {
        // Arrange
        _fileService
            .GetFileTreeAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(FailureResult());

        // Act
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Assert
        _sut.HasError.Should().BeTrue();
    }

    [Fact]
    public async Task LoadFilesCommand_WhenServiceReturnsError_SetsErrorMessage()
    {
        // Arrange
        _fileService
            .GetFileTreeAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(FailureResult("Server error"));

        // Act
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Assert
        _sut.ErrorMessage.Should().Be("Server error");
    }

    [Fact]
    public async Task LoadFilesCommand_WhenServiceReturnsErrorWithNullMessage_SetsDefaultErrorMessage()
    {
        // Arrange
        _fileService
            .GetFileTreeAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(FailureResultNullMessage());

        // Act
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Assert
        _sut.ErrorMessage.Should().Be("Failed to load files.");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // LoadFilesCommand — Loading State
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoadFilesCommand_SetsIsLoadingDuringExecution()
    {
        // Arrange
        var isLoadingDuringCall = false;
        _fileService
            .GetFileTreeAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                isLoadingDuringCall = _sut.IsLoading;
                return Task.FromResult(SuccessFiles());
            });

        // Act
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Assert
        isLoadingDuringCall.Should().BeTrue();
    }

    [Fact]
    public async Task LoadFilesCommand_ResetsIsLoadingAfterCompletion()
    {
        // Arrange
        SetupTreeReturn(BuildFile("src/foo.cs", "foo.cs"));

        // Act
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Assert
        _sut.IsLoading.Should().BeFalse();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // LoadFilesCommand — Uses CurrentPath
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoadFilesCommand_WhenCurrentPathIsNull_CallsGetFileTreeWithNull()
    {
        // Arrange
        SetupTreeReturn();

        // Act
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Assert
        await _fileService.Received(1).GetFileTreeAsync(
            Arg.Is<string?>(p => p == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadFilesCommand_WhenCurrentPathIsSet_CallsGetFileTreeWithPath()
    {
        // Arrange — navigate into a directory to set CurrentPath
        var dir = BuildDirectory("src/Models", "Models");
        SetupTreeReturn();

        await _sut.NavigateToDirectoryCommand.ExecuteAsync(dir);
        _fileService.ClearReceivedCalls();

        // Act
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Assert
        await _fileService.Received(1).GetFileTreeAsync(
            Arg.Is<string?>(p => p == "src/Models"),
            Arg.Any<CancellationToken>());
    }

    // ═════════════════════════════════════════════════════════════════════════
    // NavigateToDirectoryCommand
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task NavigateToDirectoryCommand_WhenCalled_PushesCurrentPathToBackStack()
    {
        // Arrange
        var dir = BuildDirectory("src/Models", "Models");
        SetupTreeReturn();

        // Act
        await _sut.NavigateToDirectoryCommand.ExecuteAsync(dir);

        // Assert — CanGoBack becomes true because back stack has the previous path (null)
        _sut.CanGoBack.Should().BeTrue();
    }

    [Fact]
    public async Task NavigateToDirectoryCommand_WhenCalled_SetsCurrentPathToDirectoryPath()
    {
        // Arrange
        var dir = BuildDirectory("src/Models", "Models");
        SetupTreeReturn();

        // Act
        await _sut.NavigateToDirectoryCommand.ExecuteAsync(dir);

        // Assert
        _sut.CurrentPath.Should().Be("src/Models");
    }

    [Fact]
    public async Task NavigateToDirectoryCommand_WhenCalled_ReloadsItems()
    {
        // Arrange
        var dir = BuildDirectory("src/Models", "Models");
        SetupTreeReturn(BuildFile("src/Models/Foo.cs", "Foo.cs"));

        // Act
        await _sut.NavigateToDirectoryCommand.ExecuteAsync(dir);

        // Assert — GetFileTreeAsync called with the new path
        await _fileService.Received().GetFileTreeAsync(
            Arg.Is<string?>(p => p == "src/Models"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NavigateToDirectoryCommand_WhenSearchActive_DoesNotNavigate()
    {
        // Arrange — set up search to make IsSearchActive true
        SetupTreeReturn();
        SetupSearchReturn(BuildFile("src/foo.cs", "foo.cs"));

        _sut.SearchText = "foo";
        await Task.Delay(400); // wait for debounce

        _fileService.ClearReceivedCalls();
        var dir = BuildDirectory("src/Models", "Models");

        // Act
        await _sut.NavigateToDirectoryCommand.ExecuteAsync(dir);

        // Assert — CurrentPath should not have changed to the directory
        _sut.CurrentPath.Should().BeNull();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // BackCommand
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task BackCommand_WhenBackStackHasEntries_RestoresPreviousPath()
    {
        // Arrange — navigate into a directory first
        var dir = BuildDirectory("src/Models", "Models");
        SetupTreeReturn();
        await _sut.NavigateToDirectoryCommand.ExecuteAsync(dir);
        _sut.CurrentPath.Should().Be("src/Models"); // sanity check

        // Act
        await _sut.BackCommand.ExecuteAsync(null);

        // Assert — previous path was null (root)
        _sut.CurrentPath.Should().BeNull();
    }

    [Fact]
    public async Task BackCommand_WhenBackStackHasEntries_ReloadsItems()
    {
        // Arrange
        var dir = BuildDirectory("src/Models", "Models");
        SetupTreeReturn();
        await _sut.NavigateToDirectoryCommand.ExecuteAsync(dir);
        _fileService.ClearReceivedCalls();

        // Act
        await _sut.BackCommand.ExecuteAsync(null);

        // Assert — GetFileTreeAsync called with the restored path (null = root)
        await _fileService.Received().GetFileTreeAsync(
            Arg.Is<string?>(p => p == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackCommand_WhenBackStackBecomesEmpty_SetsCanGoBackFalse()
    {
        // Arrange — navigate into one directory, then back
        var dir = BuildDirectory("src/Models", "Models");
        SetupTreeReturn();
        await _sut.NavigateToDirectoryCommand.ExecuteAsync(dir);
        _sut.CanGoBack.Should().BeTrue(); // sanity check

        // Act
        await _sut.BackCommand.ExecuteAsync(null);

        // Assert
        _sut.CanGoBack.Should().BeFalse();
    }

    [Fact]
    public async Task BackCommand_WhenBackStackIsEmpty_DoesNothing()
    {
        // Arrange — no navigation, back stack is empty
        SetupTreeReturn();
        var originalPath = _sut.CurrentPath;

        // Act
        await _sut.BackCommand.ExecuteAsync(null);

        // Assert — no exception, CurrentPath unchanged
        _sut.CurrentPath.Should().Be(originalPath);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Search — Debounce
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SearchText_WhenSetToNonEmpty_SetsIsSearchActiveTrue()
    {
        // Arrange
        SetupSearchReturn();

        // Act
        _sut.SearchText = "foo";

        // Assert
        _sut.IsSearchActive.Should().BeTrue();
    }

    [Fact]
    public void SearchText_WhenSetToEmpty_SetsIsSearchActiveFalse()
    {
        // Arrange
        SetupSearchReturn();
        SetupTreeReturn();
        _sut.SearchText = "foo"; // activate search first

        // Act
        _sut.SearchText = string.Empty;

        // Assert
        _sut.IsSearchActive.Should().BeFalse();
    }

    [Fact]
    public async Task SearchText_WhenSetToNonEmpty_CallsFindFilesAsyncAfterDelay()
    {
        // Arrange
        SetupSearchReturn(BuildFile("src/foo.cs", "foo.cs"));

        // Act
        _sut.SearchText = "foo";
        await Task.Delay(400); // wait for 300ms debounce + margin

        // Assert
        await _fileService.Received().FindFilesAsync(
            Arg.Is<string>(p => p == "*foo*"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchText_WhenChangedRapidly_OnlyCallsFindFilesAsyncOnce()
    {
        // Arrange
        SetupSearchReturn(BuildFile("src/abc.cs", "abc.cs"));

        // Act — simulate rapid typing
        _sut.SearchText = "a";
        _sut.SearchText = "ab";
        _sut.SearchText = "abc";
        await Task.Delay(400); // wait for debounce

        // Assert — FindFilesAsync should be called exactly once with the final value
        await _fileService.Received(1).FindFilesAsync(
            Arg.Is<string>(p => p == "*abc*"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchText_WhenClearedAfterSearch_ReloadsTreeAtCurrentPath()
    {
        // Arrange — perform a search first
        SetupSearchReturn(BuildFile("src/foo.cs", "foo.cs"));
        SetupTreeReturn(BuildFile("src/bar.cs", "bar.cs"));

        _sut.SearchText = "foo";
        await Task.Delay(400); // wait for search to complete
        _fileService.ClearReceivedCalls();

        // Act — clear search
        _sut.SearchText = string.Empty;
        await Task.Delay(100); // allow LoadFilesCommand to execute

        // Assert — GetFileTreeAsync called to reload tree
        await _fileService.Received().GetFileTreeAsync(
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Search — Results
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SearchText_WhenSearchReturnsResults_PopulatesItems()
    {
        // Arrange
        SetupSearchReturn(
            BuildFile("src/foo.cs", "foo.cs"),
            BuildFile("tests/foo_test.cs", "foo_test.cs"));

        // Act
        _sut.SearchText = "foo";
        await Task.Delay(400);

        // Assert
        _sut.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchText_WhenSearchReturnsError_SetsHasError()
    {
        // Arrange
        _fileService
            .FindFilesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(FailureResult("Search failed"));

        // Act
        _sut.SearchText = "foo";
        await Task.Delay(600); // 300ms debounce + margin for fire-and-forget completion

        // Assert
        _sut.HasError.Should().BeTrue();
        _sut.ErrorMessage.Should().Be("Search failed");
    }

    [Fact]
    public async Task SearchText_WhenSearchReturnsEmpty_ItemsCollectionIsEmpty()
    {
        // Arrange
        SetupSearchReturn(); // empty results

        // Act
        _sut.SearchText = "zzz_no_match";
        await Task.Delay(600); // 300ms debounce + margin for fire-and-forget completion

        // Assert
        _sut.Items.Should().BeEmpty();
        _sut.IsEmpty.Should().BeTrue();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Search — CanGoBack interaction
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CanGoBack_WhenSearchActiveAndBackStackNonEmpty_ReturnsFalse()
    {
        // Arrange — navigate into a directory, then search
        var dir = BuildDirectory("src/Models", "Models");
        SetupTreeReturn();
        SetupSearchReturn(BuildFile("src/foo.cs", "foo.cs"));

        await _sut.NavigateToDirectoryCommand.ExecuteAsync(dir);
        _sut.CanGoBack.Should().BeTrue(); // sanity check

        // Act
        _sut.SearchText = "foo";

        // Assert — CanGoBack should be false while searching
        _sut.CanGoBack.Should().BeFalse();
    }

    [Fact]
    public async Task CanGoBack_WhenSearchClearedAndBackStackNonEmpty_ReturnsTrue()
    {
        // Arrange — navigate into a directory, search, then clear search
        var dir = BuildDirectory("src/Models", "Models");
        SetupTreeReturn();
        SetupSearchReturn(BuildFile("src/foo.cs", "foo.cs"));

        await _sut.NavigateToDirectoryCommand.ExecuteAsync(dir);
        _sut.SearchText = "foo";
        _sut.CanGoBack.Should().BeFalse(); // sanity check

        // Act
        _sut.SearchText = string.Empty;

        // Assert
        _sut.CanGoBack.Should().BeTrue();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SelectFileCommand
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SelectFileCommand_WhenCallbackSet_InvokesCallbackWithRelativePath()
    {
        // Arrange
        string? capturedPath = null;
        _sut.OnFileSelected = path => capturedPath = path;
        var file = BuildFile("src/foo.cs", "foo.cs");

        // Act
        await _sut.SelectFileCommand.ExecuteAsync(file);

        // Assert
        capturedPath.Should().Be("src/foo.cs");
    }

    [Fact]
    public async Task SelectFileCommand_WhenNoCallbackSet_DoesNotThrow()
    {
        // Arrange
        var file = BuildFile("src/foo.cs", "foo.cs");

        // Act
        var act = async () => await _sut.SelectFileCommand.ExecuteAsync(file);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SelectFileCommand_PopsPopup()
    {
        // Arrange
        var file = BuildFile("src/foo.cs", "foo.cs");

        // Act
        await _sut.SelectFileCommand.ExecuteAsync(file);

        // Assert
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SelectFileCommand — Directory Routing (REQ-005 / REQ-008)
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SelectFileCommand_WhenFileIsDirectoryAndNotSearching_NavigatesIntoDirectory()
    {
        // Arrange
        SetupTreeReturn(); // return empty tree after navigation
        var dir = BuildDirectory("src/Models", "Models");

        string? capturedPath = null;
        _sut.OnFileSelected = path => capturedPath = path;

        // Act
        await _sut.SelectFileCommand.ExecuteAsync(dir);

        // Assert — navigated into directory
        _sut.CurrentPath.Should().Be("src/Models");
        capturedPath.Should().BeNull("OnFileSelected should not be invoked for directory navigation");
        await _popupService.DidNotReceive().PopPopupAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectFileCommand_WhenFileIsDirectoryAndSearching_SelectsFile()
    {
        // Arrange — activate search mode
        SetupSearchReturn(BuildFile("src/foo.cs", "foo.cs"));
        _sut.SearchText = "test";
        await Task.Delay(400); // wait for debounce

        string? capturedPath = null;
        _sut.OnFileSelected = path => capturedPath = path;

        var dir = BuildDirectory("src/Models", "Models");

        // Act
        await _sut.SelectFileCommand.ExecuteAsync(dir);

        // Assert — selected as a file, not navigated
        capturedPath.Should().Be("src/Models");
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectFileCommand_WhenFileIsRegularFile_SelectsRegardlessOfSearchState()
    {
        // Arrange
        string? capturedPath = null;
        _sut.OnFileSelected = path => capturedPath = path;
        var file = BuildFile("src/foo.cs", "foo.cs");

        // Act
        await _sut.SelectFileCommand.ExecuteAsync(file);

        // Assert
        capturedPath.Should().Be("src/foo.cs");
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
    }

    // ═════════════════════════════════════════════════════════════════════════
    // IsEmpty
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void IsEmpty_BeforeAnyLoad_DefaultsToFalse()
    {
        // Assert
        _sut.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task IsEmpty_AfterLoadWithResults_ReturnsFalse()
    {
        // Arrange
        SetupTreeReturn(BuildFile("src/foo.cs", "foo.cs"));

        // Act
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Assert
        _sut.IsEmpty.Should().BeFalse();
    }
}
