using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="AddProjectViewModel"/>.
/// </summary>
public sealed class AddProjectViewModelTests
{
    private readonly IAppPopupService _popupService;
    private readonly AddProjectViewModel _sut;

    public AddProjectViewModelTests()
    {
        _popupService = Substitute.For<IAppPopupService>();
        _sut = new AddProjectViewModel(_popupService);
    }

    // ─── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesWithEmptyFields()
    {
        // Assert
        _sut.ProjectName.Should().BeEmpty();
        _sut.ProjectPath.Should().BeEmpty();
        _sut.CanAdd.Should().BeFalse();
    }

    // ─── CanAdd ───────────────────────────────────────────────────────────────

    [Fact]
    public void CanAdd_WhenBothEmpty_IsFalse()
    {
        // Assert
        _sut.CanAdd.Should().BeFalse();
    }

    [Fact]
    public void CanAdd_WhenOnlyNameFilled_IsFalse()
    {
        // Arrange
        _sut.ProjectName = "MyProject";

        // Assert
        _sut.CanAdd.Should().BeFalse();
    }

    [Fact]
    public void CanAdd_WhenOnlyPathFilled_IsFalse()
    {
        // Arrange
        _sut.ProjectPath = "/home/user/project";

        // Assert
        _sut.CanAdd.Should().BeFalse();
    }

    [Fact]
    public void CanAdd_WhenBothFilled_IsTrue()
    {
        // Arrange
        _sut.ProjectName = "MyProject";
        _sut.ProjectPath = "/home/user/project";

        // Assert
        _sut.CanAdd.Should().BeTrue();
    }

    [Fact]
    public void CanAdd_WhenNameIsWhitespace_IsFalse()
    {
        // Arrange
        _sut.ProjectName = "   ";
        _sut.ProjectPath = "/home/user/project";

        // Assert
        _sut.CanAdd.Should().BeFalse();
    }

    // ─── AddProjectCommand ────────────────────────────────────────────────────

    [Fact]
    public async Task AddProjectCommand_ClosesPopup()
    {
        // Arrange
        _sut.ProjectName = "MyProject";
        _sut.ProjectPath = "/home/user/project";

        // Act
        await _sut.AddProjectCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddProjectCommand_ShowsToastWithProjectName()
    {
        // Arrange
        _sut.ProjectName = "MyProject";
        _sut.ProjectPath = "/home/user/project";

        // Act
        await _sut.AddProjectCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowToastAsync(
            Arg.Is<string>(s => s.Contains("MyProject")),
            Arg.Any<CancellationToken>());
    }

    // ─── CancelCommand ───────────────────────────────────────────────────────

    [Fact]
    public async Task CancelCommand_ClosesPopup()
    {
        // Act
        await _sut.CancelCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
    }
}
