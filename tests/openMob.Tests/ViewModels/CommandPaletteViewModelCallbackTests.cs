using openMob.Core.Models;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for the callback mode of <see cref="CommandPaletteViewModel"/>.
/// Covers the dual-mode behaviour of <c>ExecuteCommandCommand</c>:
/// when <c>OnCommandSelected</c> is set (callback mode) vs. null (direct execution mode).
/// </summary>
public sealed class CommandPaletteViewModelCallbackTests
{
    private readonly ICommandService _commandService;
    private readonly IAppPopupService _popupService;
    private readonly CommandPaletteViewModel _sut;

    public CommandPaletteViewModelCallbackTests()
    {
        _commandService = Substitute.For<ICommandService>();
        _popupService = Substitute.For<IAppPopupService>();

        _sut = new CommandPaletteViewModel(_commandService, _popupService);
    }

    // ─── Callback Mode ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteCommandCommand_WhenOnCommandSelectedIsSet_InvokesCallbackWithCommandName()
    {
        // Arrange
        string? capturedCommandName = null;
        _sut.OnCommandSelected = name => capturedCommandName = name;
        var command = new CommandItem("test", "Run tests", false);

        // Act
        await _sut.ExecuteCommandCommand.ExecuteAsync(command);

        // Assert
        capturedCommandName.Should().Be("test");
    }

    [Fact]
    public async Task ExecuteCommandCommand_WhenOnCommandSelectedIsSet_PopsPopup()
    {
        // Arrange
        _sut.OnCommandSelected = _ => { };
        var command = new CommandItem("test", "Run tests", false);

        // Act
        await _sut.ExecuteCommandCommand.ExecuteAsync(command);

        // Assert
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteCommandCommand_WhenOnCommandSelectedIsSet_DoesNotCallCommandService()
    {
        // Arrange
        _sut.OnCommandSelected = _ => { };
        _sut.CurrentSessionId = "sess-1";
        var command = new CommandItem("test", "Run tests", false);

        // Act
        await _sut.ExecuteCommandCommand.ExecuteAsync(command);

        // Assert
        await _commandService.DidNotReceive().ExecuteCommandAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ─── Direct Execution Mode (OnCommandSelected is null) ───────────────────

    [Fact]
    public async Task ExecuteCommandCommand_WhenOnCommandSelectedIsNull_CallsCommandService()
    {
        // Arrange
        _sut.OnCommandSelected = null;
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
    public async Task ExecuteCommandCommand_WhenOnCommandSelectedIsNull_PopsPopup()
    {
        // Arrange
        _sut.OnCommandSelected = null;
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
    public async Task ExecuteCommandCommand_WhenOnCommandSelectedIsNullAndNoSession_ShowsError()
    {
        // Arrange
        _sut.OnCommandSelected = null;
        _sut.CurrentSessionId = null;
        var command = new CommandItem("test", "Run tests", false);

        // Act
        await _sut.ExecuteCommandCommand.ExecuteAsync(command);

        // Assert
        await _popupService.Received(1).ShowErrorAsync(
            "Error", "No active session.", Arg.Any<CancellationToken>());
    }
}
