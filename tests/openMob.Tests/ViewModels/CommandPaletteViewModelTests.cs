using NSubstitute.ExceptionExtensions;
using openMob.Core.Models;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="CommandPaletteViewModel"/>.
/// Covers command loading, searching, execution, refresh, and empty state.
/// </summary>
public sealed class CommandPaletteViewModelTests
{
    private readonly ICommandService _commandService;
    private readonly IAppPopupService _popupService;
    private readonly CommandPaletteViewModel _sut;

    public CommandPaletteViewModelTests()
    {
        _commandService = Substitute.For<ICommandService>();
        _popupService = Substitute.For<IAppPopupService>();

        _sut = new CommandPaletteViewModel(_commandService, _popupService);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static IReadOnlyList<CommandItem> BuildCommands()
    {
        return new List<CommandItem>
        {
            new("test", "Run tests", false),
            new("lint", "Lint code", true),
            new("deploy", "Deploy app", false),
        };
    }

    // ─── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Assert
        _sut.Commands.Should().BeEmpty();
        _sut.SearchText.Should().BeEmpty();
        _sut.IsLoading.Should().BeFalse();
        _sut.CurrentSessionId.Should().BeNull();
    }

    // ─── LoadCommandsCommand ─────────────────────────────────────────────────

    [Fact]
    public async Task LoadCommandsCommand_WhenExecuted_PopulatesCommandsCollection()
    {
        // Arrange
        var commands = BuildCommands();
        _commandService.GetCommandsAsync(Arg.Any<CancellationToken>()).Returns(commands);

        // Act
        await _sut.LoadCommandsCommand.ExecuteAsync(null);

        // Assert
        _sut.Commands.Should().HaveCount(3);
        _sut.Commands.Should().Contain(c => c.Name == "test");
    }

    [Fact]
    public async Task LoadCommandsCommand_WhenExecuted_SetsIsLoadingDuringLoad()
    {
        // Arrange
        var isLoadingDuringCall = false;
        _commandService.GetCommandsAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                isLoadingDuringCall = _sut.IsLoading;
                return Task.FromResult<IReadOnlyList<CommandItem>>(BuildCommands());
            });

        // Act
        await _sut.LoadCommandsCommand.ExecuteAsync(null);

        // Assert
        isLoadingDuringCall.Should().BeTrue();
        _sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadCommandsCommand_WhenServiceFails_SetsCommandsToEmpty()
    {
        // Arrange
        _commandService.GetCommandsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        await _sut.LoadCommandsCommand.ExecuteAsync(null);

        // Assert
        _sut.Commands.Should().BeEmpty();
        _sut.IsLoading.Should().BeFalse();
    }

    // ─── IsEmpty ─────────────────────────────────────────────────────────────

    [Fact]
    public void IsEmpty_WhenNoCommandsAndNotLoading_ReturnsTrue()
    {
        // Assert
        _sut.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task IsEmpty_WhenCommandsExist_ReturnsFalse()
    {
        // Arrange
        _commandService.GetCommandsAsync(Arg.Any<CancellationToken>()).Returns(BuildCommands());

        // Act
        await _sut.LoadCommandsCommand.ExecuteAsync(null);

        // Assert
        _sut.IsEmpty.Should().BeFalse();
    }

    // ─── ExecuteCommandCommand ───────────────────────────────────────────────

    [Fact]
    public async Task ExecuteCommandCommand_WhenCalled_CallsCommandService()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        var command = new CommandItem("test", "Run tests", false);
        _commandService.ExecuteCommandAsync("sess-1", "test", Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<bool>.Ok(true));

        // Act
        await _sut.ExecuteCommandCommand.ExecuteAsync(command);

        // Assert
        await _commandService.Received(1).ExecuteCommandAsync(
            "sess-1", "test", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteCommandCommand_WhenCalled_DismissesSheet()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        var command = new CommandItem("test", "Run tests", false);
        _commandService.ExecuteCommandAsync("sess-1", "test", Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<bool>.Ok(true));

        // Act
        await _sut.ExecuteCommandCommand.ExecuteAsync(command);

        // Assert
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteCommandCommand_WhenNoSessionId_ShowsError()
    {
        // Arrange
        _sut.CurrentSessionId = null;
        var command = new CommandItem("test", "Run tests", false);

        // Act
        await _sut.ExecuteCommandCommand.ExecuteAsync(command);

        // Assert
        await _popupService.Received(1).ShowErrorAsync(
            "Error", "No active session.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteCommandCommand_WhenCommandFails_ShowsError()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        var command = new CommandItem("test", "Run tests", false);
        var error = new ChatServiceError(ChatServiceErrorKind.ServerError, "Command failed");
        _commandService.ExecuteCommandAsync("sess-1", "test", Arg.Any<CancellationToken>())
            .Returns(ChatServiceResult<bool>.Fail(error));

        // Act
        await _sut.ExecuteCommandCommand.ExecuteAsync(command);

        // Assert
        await _popupService.Received(1).ShowErrorAsync(
            "Command Failed", "Command failed", Arg.Any<CancellationToken>());
    }

    // ─── RefreshCommandsCommand ──────────────────────────────────────────────

    [Fact]
    public async Task RefreshCommandsCommand_WhenCalled_InvalidatesCacheAndReloads()
    {
        // Arrange
        _commandService.GetCommandsAsync(Arg.Any<CancellationToken>()).Returns(BuildCommands());

        // Act
        await _sut.RefreshCommandsCommand.ExecuteAsync(null);

        // Assert
        _commandService.Received(1).InvalidateCache();
        await _commandService.Received(1).GetCommandsAsync(Arg.Any<CancellationToken>());
    }

    // ─── SearchText filtering ────────────────────────────────────────────────

    [Fact]
    public async Task OnSearchTextChanged_WhenTextEntered_FiltersCommands()
    {
        // Arrange
        var filtered = new List<CommandItem> { new("test", "Run tests", false) };
        _commandService.SearchCommandsAsync("test", Arg.Any<CancellationToken>())
            .Returns(filtered);

        // Act
        _sut.SearchText = "test";
        // Allow the async filter to complete
        await Task.Delay(100);

        // Assert
        await _commandService.Received().SearchCommandsAsync("test", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnSearchTextChanged_WhenTextCleared_CallsSearchWithEmptyString()
    {
        // Arrange — first set a non-empty search text, then clear it
        var filtered = new List<CommandItem> { new("test", "Run tests", false) };
        _commandService.SearchCommandsAsync("test", Arg.Any<CancellationToken>())
            .Returns(filtered);
        _commandService.SearchCommandsAsync("", Arg.Any<CancellationToken>())
            .Returns(BuildCommands());

        _sut.SearchText = "test";
        await Task.Delay(100);

        // Act — clear the search text
        _sut.SearchText = "";
        await Task.Delay(100);

        // Assert
        await _commandService.Received().SearchCommandsAsync("", Arg.Any<CancellationToken>());
    }
}
