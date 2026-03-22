using openMob.Core.Infrastructure.Http;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="FilePickerViewModel"/>.
/// Covers file loading, search filtering, file selection callback, error handling,
/// loading state, and empty state.
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

    private static OpencodeResult<IReadOnlyList<FileDto>> BuildSuccessResult(params FileDto[] files)
        => OpencodeResult<IReadOnlyList<FileDto>>.Success(files.ToList().AsReadOnly());

    private static FileDto BuildFile(string relativePath, string name)
        => new(relativePath, name);

    // ─── Constructor ─────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Assert
        _sut.Files.Should().BeEmpty();
        _sut.FilteredFiles.Should().BeEmpty();
        _sut.SearchText.Should().BeEmpty();
        _sut.IsLoading.Should().BeFalse();
        _sut.HasError.Should().BeFalse();
        _sut.ErrorMessage.Should().BeNull();
    }

    // ─── LoadFilesCommand — Happy Path ───────────────────────────────────────

    [Fact]
    public async Task LoadFilesCommand_WhenServiceReturnsFiles_PopulatesFilesCollection()
    {
        // Arrange
        var files = new[]
        {
            BuildFile("src/foo.cs", "foo.cs"),
            BuildFile("README.md", "README.md"),
        };
        _fileService.GetFilesAsync(Arg.Any<CancellationToken>()).Returns(BuildSuccessResult(files));

        // Act
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Assert
        _sut.Files.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadFilesCommand_WhenServiceReturnsFiles_PopulatesFilteredFiles()
    {
        // Arrange
        var files = new[]
        {
            BuildFile("src/foo.cs", "foo.cs"),
            BuildFile("README.md", "README.md"),
        };
        _fileService.GetFilesAsync(Arg.Any<CancellationToken>()).Returns(BuildSuccessResult(files));

        // Act
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Assert
        _sut.FilteredFiles.Should().HaveCount(2);
    }

    // ─── LoadFilesCommand — Error Path ───────────────────────────────────────

    [Fact]
    public async Task LoadFilesCommand_WhenServiceReturnsError_SetsHasError()
    {
        // Arrange
        var error = new OpencodeApiError(ErrorKind.ServerError, "Server error", 500, null);
        _fileService.GetFilesAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<FileDto>>.Failure(error));

        // Act
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Assert
        _sut.HasError.Should().BeTrue();
    }

    [Fact]
    public async Task LoadFilesCommand_WhenServiceReturnsError_SetsErrorMessage()
    {
        // Arrange
        var error = new OpencodeApiError(ErrorKind.ServerError, "Server error", 500, null);
        _fileService.GetFilesAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<FileDto>>.Failure(error));

        // Act
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Assert
        _sut.ErrorMessage.Should().Be("Server error");
    }

    [Fact]
    public async Task LoadFilesCommand_WhenServiceReturnsErrorWithNullMessage_SetsDefaultErrorMessage()
    {
        // Arrange
        var error = new OpencodeApiError(ErrorKind.ServerError, null!, 500, null);
        _fileService.GetFilesAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<FileDto>>.Failure(error));

        // Act
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Assert
        _sut.ErrorMessage.Should().Be("Failed to load files.");
    }

    // ─── LoadFilesCommand — Loading State ────────────────────────────────────

    [Fact]
    public async Task LoadFilesCommand_SetsIsLoadingDuringExecution()
    {
        // Arrange
        var isLoadingDuringCall = false;
        _fileService.GetFilesAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                isLoadingDuringCall = _sut.IsLoading;
                return Task.FromResult(BuildSuccessResult());
            });

        // Act
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Assert
        isLoadingDuringCall.Should().BeTrue();
        _sut.IsLoading.Should().BeFalse();
    }

    // ─── SearchText — Filtering ──────────────────────────────────────────────

    [Fact]
    public async Task SearchText_WhenSetToMatchingPath_FiltersFilteredFilesByRelativePath()
    {
        // Arrange
        var files = new[]
        {
            BuildFile("src/foo.cs", "foo.cs"),
            BuildFile("tests/bar.cs", "bar.cs"),
            BuildFile("README.md", "README.md"),
        };
        _fileService.GetFilesAsync(Arg.Any<CancellationToken>()).Returns(BuildSuccessResult(files));
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Act
        _sut.SearchText = "src";

        // Assert
        _sut.FilteredFiles.Should().ContainSingle();
        _sut.FilteredFiles[0].RelativePath.Should().Be("src/foo.cs");
    }

    [Fact]
    public async Task SearchText_WhenSetToMatchingName_FiltersFilteredFilesByName()
    {
        // Arrange
        var files = new[]
        {
            BuildFile("src/foo.cs", "foo.cs"),
            BuildFile("tests/bar.cs", "bar.cs"),
        };
        _fileService.GetFilesAsync(Arg.Any<CancellationToken>()).Returns(BuildSuccessResult(files));
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Act
        _sut.SearchText = "bar";

        // Assert
        _sut.FilteredFiles.Should().ContainSingle();
        _sut.FilteredFiles[0].Name.Should().Be("bar.cs");
    }

    [Fact]
    public async Task SearchText_IsCaseInsensitive()
    {
        // Arrange
        var files = new[]
        {
            BuildFile("src/Foo.cs", "Foo.cs"),
            BuildFile("tests/bar.cs", "bar.cs"),
        };
        _fileService.GetFilesAsync(Arg.Any<CancellationToken>()).Returns(BuildSuccessResult(files));
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Act
        _sut.SearchText = "foo";

        // Assert
        _sut.FilteredFiles.Should().ContainSingle();
        _sut.FilteredFiles[0].Name.Should().Be("Foo.cs");
    }

    [Fact]
    public async Task SearchText_WhenEmpty_ShowsAllFiles()
    {
        // Arrange
        var files = new[]
        {
            BuildFile("src/foo.cs", "foo.cs"),
            BuildFile("tests/bar.cs", "bar.cs"),
        };
        _fileService.GetFilesAsync(Arg.Any<CancellationToken>()).Returns(BuildSuccessResult(files));
        await _sut.LoadFilesCommand.ExecuteAsync(null);
        _sut.SearchText = "foo"; // filter first

        // Act
        _sut.SearchText = string.Empty;

        // Assert
        _sut.FilteredFiles.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchText_WhenNoMatch_ReturnsEmptyFilteredFiles()
    {
        // Arrange
        var files = new[]
        {
            BuildFile("src/foo.cs", "foo.cs"),
        };
        _fileService.GetFilesAsync(Arg.Any<CancellationToken>()).Returns(BuildSuccessResult(files));
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Act
        _sut.SearchText = "zzz_no_match";

        // Assert
        _sut.FilteredFiles.Should().BeEmpty();
    }

    // ─── SelectFileCommand ───────────────────────────────────────────────────

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

    // ─── IsEmpty ─────────────────────────────────────────────────────────────

    [Fact]
    public void IsEmpty_BeforeAnyLoad_DefaultsToFalse()
    {
        // Assert — IsEmpty defaults to false before any load (set by ApplyFilter only)
        _sut.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task IsEmpty_AfterSearchFilterYieldsNoResults_ReturnsTrue()
    {
        // Arrange — load files first, then filter to empty
        var files = new[] { BuildFile("src/foo.cs", "foo.cs") };
        _fileService.GetFilesAsync(Arg.Any<CancellationToken>()).Returns(BuildSuccessResult(files));
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Act — search for something that doesn't match
        _sut.SearchText = "zzz_no_match_at_all";

        // Assert
        _sut.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task IsEmpty_WhenFilteredFilesExist_ReturnsFalse()
    {
        // Arrange
        var files = new[] { BuildFile("src/foo.cs", "foo.cs") };
        _fileService.GetFilesAsync(Arg.Any<CancellationToken>()).Returns(BuildSuccessResult(files));

        // Act
        await _sut.LoadFilesCommand.ExecuteAsync(null);

        // Assert
        _sut.IsEmpty.Should().BeFalse();
    }
}
