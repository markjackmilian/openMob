using openMob.Core.Infrastructure.Http;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="FolderPickerViewModel"/>.
/// </summary>
public sealed class FolderPickerViewModelTests
{
    private readonly IFileService _fileService;
    private readonly IAppPopupService _popupService;
    private readonly FolderPickerViewModel _sut;

    public FolderPickerViewModelTests()
    {
        _fileService = Substitute.For<IFileService>();
        _popupService = Substitute.For<IAppPopupService>();
        _sut = new FolderPickerViewModel(_fileService, _popupService);
    }

    private static OpencodeResult<IReadOnlyList<FileDto>> SuccessFiles(params FileDto[] files)
        => OpencodeResult<IReadOnlyList<FileDto>>.Success(files.ToList().AsReadOnly());

    private static FileDto BuildDirectory(string relativePath, string name, bool ignored = false)
        => new(relativePath, name, "directory", ignored);

    private static FileDto BuildFile(string relativePath, string name, bool ignored = false)
        => new(relativePath, name, "file", ignored);

    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Assert
        _sut.Items.Should().BeEmpty();
        _sut.CurrentPath.Should().BeNull();
        _sut.IsLoading.Should().BeFalse();
        _sut.IsEmpty.Should().BeFalse();
        _sut.HasError.Should().BeFalse();
        _sut.ErrorMessage.Should().BeNull();
        _sut.CanGoBack.Should().BeFalse();
        _sut.CanConfirm.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WhenServiceReturnsFolders_PopulatesDirectoriesOnly()
    {
        // Arrange
        _fileService.GetFileTreeAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(SuccessFiles(
                BuildDirectory("alpha", "alpha"),
                BuildFile("README.md", "README.md"),
                BuildDirectory("ignored", "ignored", ignored: true)));

        // Act
        await _sut.InitializeAsync("/worktree");

        // Assert
        _sut.CurrentPath.Should().Be("/worktree");
        _sut.Items.Should().ContainSingle(item => item.Name == "alpha");
        _sut.Items.Should().NotContain(item => item.Type == "file");
        _sut.Items.Should().NotContain(item => item.IsIgnored);
    }

    [Fact]
    public async Task SelectFolderCommand_WhenFolderSelected_NavigatesIntoFolder()
    {
        // Arrange
        _fileService.GetFileTreeAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(SuccessFiles(BuildDirectory("alpha", "alpha")));

        await _sut.InitializeAsync("/worktree");
        _fileService.ClearReceivedCalls();

        // Act
        await _sut.SelectFolderCommand.ExecuteAsync(_sut.Items[0]);

        // Assert
        _sut.CurrentPath.Should().Be("alpha");
        await _fileService.Received(1).GetFileTreeAsync("alpha", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackCommand_WhenBackStackHasEntries_RestoresPreviousPath()
    {
        // Arrange
        _fileService.GetFileTreeAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(SuccessFiles(BuildDirectory("alpha", "alpha")));

        await _sut.InitializeAsync("/worktree");
        await _sut.SelectFolderCommand.ExecuteAsync(_sut.Items[0]);
        _fileService.ClearReceivedCalls();

        // Act
        await _sut.BackCommand.ExecuteAsync(null);

        // Assert
        _sut.CurrentPath.Should().Be("/worktree");
        await _fileService.Received(1).GetFileTreeAsync("/worktree", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmCommand_WhenExecuted_PopsPopupAndInvokesCallback()
    {
        // Arrange
        string? selectedPath = null;
        _sut.OnFolderSelected = (path, _) =>
        {
            selectedPath = path;
            return Task.CompletedTask;
        };
        _fileService.GetFileTreeAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(SuccessFiles(BuildDirectory("alpha", "alpha")));

        await _sut.InitializeAsync("/worktree");

        // Act
        await _sut.ConfirmCommand.ExecuteAsync(null);

        // Assert
        selectedPath.Should().Be("/worktree");
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
    }
}
