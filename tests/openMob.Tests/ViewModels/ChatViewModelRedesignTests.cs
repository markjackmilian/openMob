using System.Text.Json;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Models;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for the Chat Page Redesign extensions to <see cref="ChatViewModel"/>.
/// Covers new properties (ThinkingLevel, AutoAccept, IsSubagentActive, SubagentName,
/// IsContextBarVisible), new commands (RenameSession, OpenContextSheet, OpenCommandPalette),
/// and scroll direction handling.
/// </summary>
public sealed class ChatViewModelRedesignTests : IDisposable
{
    private readonly IProjectService _projectService;
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;
    private readonly IOpencodeConnectionManager _connectionManager;
    private readonly IProviderService _providerService;
    private readonly IProjectPreferenceService _preferenceService;
    private readonly IChatService _chatService;
    private readonly IOpencodeApiClient _apiClient;
    private readonly IDispatcherService _dispatcher;
    private readonly ChatViewModel _sut;

    public ChatViewModelRedesignTests()
    {
        _projectService = Substitute.For<IProjectService>();
        _sessionService = Substitute.For<ISessionService>();
        _navigationService = Substitute.For<INavigationService>();
        _popupService = Substitute.For<IAppPopupService>();
        _connectionManager = Substitute.For<IOpencodeConnectionManager>();
        _providerService = Substitute.For<IProviderService>();
        _preferenceService = Substitute.For<IProjectPreferenceService>();
        _chatService = Substitute.For<IChatService>();
        _apiClient = Substitute.For<IOpencodeApiClient>();
        _dispatcher = Substitute.For<IDispatcherService>();

        // CRITICAL: IDispatcherService mock must execute the action synchronously
        _dispatcher.When(d => d.Dispatch(Arg.Any<Action>())).Do(ci => ci.Arg<Action>()());

        // Default: server connected, provider configured
        _connectionManager.ConnectionStatus.Returns(ServerConnectionStatus.Connected);
        _providerService.HasAnyProviderConfiguredAsync(Arg.Any<CancellationToken>()).Returns(true);

        _sut = new ChatViewModel(
            _projectService,
            _sessionService,
            _navigationService,
            _popupService,
            _connectionManager,
            _providerService,
            _preferenceService,
            _chatService,
            _apiClient,
            _dispatcher);
    }

    public void Dispose()
    {
        // Unregister the SUT from the messenger to avoid test pollution
        _sut.Dispose();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

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

    // ─── Default property values ─────────────────────────────────────────────

    [Fact]
    public void ThinkingLevel_DefaultValue_IsMedium()
    {
        // Assert
        _sut.ThinkingLevel.Should().Be(ThinkingLevel.Medium);
    }

    [Fact]
    public void AutoAccept_DefaultValue_IsFalse()
    {
        // Assert
        _sut.AutoAccept.Should().BeFalse();
    }

    [Fact]
    public void IsSubagentActive_DefaultValue_IsFalse()
    {
        // Assert
        _sut.IsSubagentActive.Should().BeFalse();
    }

    [Fact]
    public void SubagentName_DefaultValue_IsEmptyString()
    {
        // Assert
        _sut.SubagentName.Should().BeEmpty();
    }

    [Fact]
    public void IsContextBarVisible_DefaultValue_IsTrue()
    {
        // Assert
        _sut.IsContextBarVisible.Should().BeTrue();
    }

    // ─── RenameSessionCommand ────────────────────────────────────────────────

    [Fact]
    public async Task RenameSessionCommand_WhenUserConfirms_UpdatesSessionName()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        _sut.SessionName = "Old Name";
        _popupService.ShowRenameAsync("Old Name", Arg.Any<CancellationToken>())
            .Returns("New Name");
        _sessionService.UpdateSessionTitleAsync("sess-1", "New Name", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _sut.RenameSessionCommand.ExecuteAsync(null);

        // Assert
        _sut.SessionName.Should().Be("New Name");
    }

    [Fact]
    public async Task RenameSessionCommand_WhenUserConfirms_ShowsToast()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        _sut.SessionName = "Old Name";
        _popupService.ShowRenameAsync("Old Name", Arg.Any<CancellationToken>())
            .Returns("New Name");
        _sessionService.UpdateSessionTitleAsync("sess-1", "New Name", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _sut.RenameSessionCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowToastAsync("Session renamed.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenameSessionCommand_WhenUserCancels_DoesNotChangeSessionName()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        _sut.SessionName = "Original Name";
        _popupService.ShowRenameAsync("Original Name", Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        await _sut.RenameSessionCommand.ExecuteAsync(null);

        // Assert
        _sut.SessionName.Should().Be("Original Name");
    }

    [Fact]
    public async Task RenameSessionCommand_WhenUserEntersSameName_DoesNotCallService()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        _sut.SessionName = "Same Name";
        _popupService.ShowRenameAsync("Same Name", Arg.Any<CancellationToken>())
            .Returns("Same Name");

        // Act
        await _sut.RenameSessionCommand.ExecuteAsync(null);

        // Assert
        await _sessionService.DidNotReceive().UpdateSessionTitleAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenameSessionCommand_WhenServiceFails_ShowsError()
    {
        // Arrange
        _sut.CurrentSessionId = "sess-1";
        _sut.SessionName = "Old Name";
        _popupService.ShowRenameAsync("Old Name", Arg.Any<CancellationToken>())
            .Returns("New Name");
        _sessionService.UpdateSessionTitleAsync("sess-1", "New Name", Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.RenameSessionCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowErrorAsync(
            "Error", "Failed to rename the session.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenameSessionCommand_WhenNoSessionId_DoesNotCallPopup()
    {
        // Arrange — CurrentSessionId is null by default

        // Act
        await _sut.RenameSessionCommand.ExecuteAsync(null);

        // Assert
        await _popupService.DidNotReceive().ShowRenameAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ─── OpenContextSheetCommand ─────────────────────────────────────────────

    [Fact]
    public async Task OpenContextSheetCommand_WhenProjectIdIsSet_CallsPopupService()
    {
        // Arrange
        _sut.CurrentProjectId = "proj-1";
        _popupService.ShowContextSheetAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.OpenContextSheetCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowContextSheetAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ─── OpenCommandPaletteCommand ───────────────────────────────────────────

    [Fact]
    public async Task OpenCommandPaletteCommand_WhenExecuted_CallsPopupService()
    {
        // Arrange
        _popupService.ShowCommandPaletteAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.OpenCommandPaletteCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowCommandPaletteAsync(Arg.Any<CancellationToken>());
    }

    // ─── OnScrollDirectionChanged ────────────────────────────────────────────

    [Fact]
    public void OnScrollDirectionChanged_WhenScrollingDown_SetsIsContextBarVisibleFalse()
    {
        // Arrange — add a message so IsEmpty is false
        _sut.Messages.Add(ChatMessage.CreateOptimistic("sess-1", "test"));
        _sut.IsEmpty = false;

        // Act
        _sut.OnScrollDirectionChanged(isScrollingDown: true);

        // Assert
        _sut.IsContextBarVisible.Should().BeFalse();
    }

    [Fact]
    public void OnScrollDirectionChanged_WhenScrollingUp_SetsIsContextBarVisibleTrue()
    {
        // Arrange — add a message so IsEmpty is false, then hide the bar
        _sut.Messages.Add(ChatMessage.CreateOptimistic("sess-1", "test"));
        _sut.IsEmpty = false;
        _sut.IsContextBarVisible = false;

        // Act
        _sut.OnScrollDirectionChanged(isScrollingDown: false);

        // Assert
        _sut.IsContextBarVisible.Should().BeTrue();
    }

    [Fact]
    public void OnScrollDirectionChanged_WhenIsEmpty_KeepsIsContextBarVisibleTrue()
    {
        // Arrange — IsEmpty is true by default

        // Act
        _sut.OnScrollDirectionChanged(isScrollingDown: true);

        // Assert
        _sut.IsContextBarVisible.Should().BeTrue();
    }

    [Fact]
    public void OnScrollDirectionChanged_WhenIsEmptyAndScrollingUp_KeepsIsContextBarVisibleTrue()
    {
        // Arrange — IsEmpty is true by default

        // Act
        _sut.OnScrollDirectionChanged(isScrollingDown: false);

        // Assert
        _sut.IsContextBarVisible.Should().BeTrue();
    }
}
