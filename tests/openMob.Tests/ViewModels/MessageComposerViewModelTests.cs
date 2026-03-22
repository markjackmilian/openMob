using CommunityToolkit.Mvvm.Messaging;
using openMob.Core.Data.Entities;
using openMob.Core.Messages;
using openMob.Core.Models;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="MessageComposerViewModel"/>.
/// Covers initialisation, session controls, token insertion, send/close commands,
/// streaming guard, draft persistence, messenger integration, and disposal.
/// </summary>
[Collection(MessengerTestCollection.Name)]
public sealed class MessageComposerViewModelTests : IDisposable
{
    private readonly IProjectPreferenceService _preferenceService;
    private readonly IAppPopupService _popupService;
    private readonly IDraftService _draftService;
    private readonly IDispatcherService _dispatcher;
    private readonly MessageComposerViewModel _sut;

    public MessageComposerViewModelTests()
    {
        _preferenceService = Substitute.For<IProjectPreferenceService>();
        _popupService = Substitute.For<IAppPopupService>();
        _draftService = Substitute.For<IDraftService>();
        _dispatcher = Substitute.For<IDispatcherService>();

        // CRITICAL: IDispatcherService mock must execute the action synchronously
        _dispatcher.When(d => d.Dispatch(Arg.Any<Action>())).Do(ci => ci.Arg<Action>().Invoke());

        _sut = new MessageComposerViewModel(
            _preferenceService,
            _popupService,
            _draftService,
            _dispatcher);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private ProjectPreference BuildPreference(
        string? agentName = "test-agent",
        ThinkingLevel thinkingLevel = ThinkingLevel.High,
        bool autoAccept = true)
        => new()
        {
            ProjectId = "proj-1",
            AgentName = agentName,
            ThinkingLevel = thinkingLevel,
            AutoAccept = autoAccept,
        };

    private async Task InitializeSutAsync(
        string projectId = "proj-1",
        string sessionId = "sess-1",
        bool isStreaming = false,
        ProjectPreference? preference = null)
    {
        var pref = preference ?? BuildPreference();
        _preferenceService.GetOrDefaultAsync(projectId, Arg.Any<CancellationToken>()).Returns(pref);
        _draftService.GetDraft(sessionId).Returns((string?)null);
        await _sut.InitializeAsync(projectId, sessionId, isStreaming);
    }

    // ─── Constructor ─────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Assert
        _sut.MessageText.Should().BeEmpty();
        _sut.ProjectId.Should().BeEmpty();
        _sut.SessionId.Should().BeEmpty();
        _sut.IsStreaming.Should().BeFalse();
        _sut.IsThinkLevelExpanded.Should().BeFalse();
    }

    // ─── InitializeAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_SetsProjectIdAndSessionId()
    {
        // Arrange & Act
        await InitializeSutAsync(projectId: "proj-1", sessionId: "sess-1");

        // Assert
        _sut.ProjectId.Should().Be("proj-1");
        _sut.SessionId.Should().Be("sess-1");
    }

    [Fact]
    public async Task InitializeAsync_LoadsPreferencesAndSetsSessionAgentName()
    {
        // Arrange & Act
        await InitializeSutAsync(preference: BuildPreference(agentName: "my-agent"));

        // Assert
        _sut.SessionAgentName.Should().Be("my-agent");
    }

    [Fact]
    public async Task InitializeAsync_LoadsPreferencesAndSetsSessionThinkingLevel()
    {
        // Arrange & Act
        await InitializeSutAsync(preference: BuildPreference(thinkingLevel: ThinkingLevel.Low));

        // Assert
        _sut.SessionThinkingLevel.Should().Be(ThinkingLevel.Low);
    }

    [Fact]
    public async Task InitializeAsync_LoadsPreferencesAndSetsSessionAutoAccept()
    {
        // Arrange & Act
        await InitializeSutAsync(preference: BuildPreference(autoAccept: true));

        // Assert
        _sut.SessionAutoAccept.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_SetsIsStreamingFromParameter()
    {
        // Arrange & Act
        await InitializeSutAsync(isStreaming: true);

        // Assert
        _sut.IsStreaming.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WhenDraftExists_RestoresMessageText()
    {
        // Arrange
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference());
        _draftService.GetDraft("sess-1").Returns("Saved draft text");

        // Act
        await _sut.InitializeAsync("proj-1", "sess-1", false);

        // Assert
        _sut.MessageText.Should().Be("Saved draft text");
    }

    [Fact]
    public async Task InitializeAsync_WhenNoDraftExists_SetsMessageTextToEmpty()
    {
        // Arrange
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference());
        _draftService.GetDraft("sess-1").Returns((string?)null);

        // Act
        await _sut.InitializeAsync("proj-1", "sess-1", false);

        // Assert
        _sut.MessageText.Should().BeEmpty();
    }

    // ─── SessionAgentDisplayName ─────────────────────────────────────────────

    [Fact]
    public async Task SessionAgentDisplayName_WhenAgentNameIsNull_ReturnsDefault()
    {
        // Arrange & Act
        await InitializeSutAsync(preference: BuildPreference(agentName: null));

        // Assert
        _sut.SessionAgentDisplayName.Should().Be("Default");
    }

    [Fact]
    public async Task SessionAgentDisplayName_WhenAgentNameIsSet_ReturnsAgentName()
    {
        // Arrange & Act
        await InitializeSutAsync(preference: BuildPreference(agentName: "my-agent"));

        // Assert
        _sut.SessionAgentDisplayName.Should().Be("my-agent");
    }

    // ─── SelectAgentCommand ──────────────────────────────────────────────────

    [Fact]
    public async Task SelectAgentCommand_CallsShowAgentPickerAsync()
    {
        // Arrange
        await InitializeSutAsync();

        // Act
        await _sut.SelectAgentCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowAgentPickerAsync(
            Arg.Any<Action<string?>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectAgentCommand_WhenAgentSelected_UpdatesSessionAgentName()
    {
        // Arrange
        await InitializeSutAsync();
        _popupService.ShowAgentPickerAsync(Arg.Any<Action<string?>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callInfo.Arg<Action<string?>>()("new-agent");
                return Task.CompletedTask;
            });

        // Act
        await _sut.SelectAgentCommand.ExecuteAsync(null);

        // Assert
        _sut.SessionAgentName.Should().Be("new-agent");
    }

    // ─── SetThinkingLevelCommand ─────────────────────────────────────────────

    [Theory]
    [InlineData(ThinkingLevel.Low)]
    [InlineData(ThinkingLevel.Medium)]
    [InlineData(ThinkingLevel.High)]
    public async Task SetThinkingLevelCommand_SetsSessionThinkingLevel(ThinkingLevel level)
    {
        // Arrange
        await InitializeSutAsync();

        // Act
        _sut.SetThinkingLevelCommand.Execute(level);

        // Assert
        _sut.SessionThinkingLevel.Should().Be(level);
    }

    [Fact]
    public async Task SetThinkingLevelCommand_CollapsesIsThinkLevelExpanded()
    {
        // Arrange
        await InitializeSutAsync();
        _sut.ToggleThinkLevelExpandedCommand.Execute(null); // expand first

        // Act
        _sut.SetThinkingLevelCommand.Execute(ThinkingLevel.Low);

        // Assert
        _sut.IsThinkLevelExpanded.Should().BeFalse();
    }

    // ─── ToggleThinkLevelExpandedCommand ─────────────────────────────────────

    [Fact]
    public void ToggleThinkLevelExpandedCommand_TogglesIsThinkLevelExpanded()
    {
        // Arrange
        _sut.IsThinkLevelExpanded.Should().BeFalse();

        // Act
        _sut.ToggleThinkLevelExpandedCommand.Execute(null);

        // Assert
        _sut.IsThinkLevelExpanded.Should().BeTrue();
    }

    [Fact]
    public void ToggleThinkLevelExpandedCommand_WhenCalledTwice_ReturnsFalse()
    {
        // Act
        _sut.ToggleThinkLevelExpandedCommand.Execute(null);
        _sut.ToggleThinkLevelExpandedCommand.Execute(null);

        // Assert
        _sut.IsThinkLevelExpanded.Should().BeFalse();
    }

    // ─── ToggleAutoAcceptCommand ─────────────────────────────────────────────

    [Fact]
    public async Task ToggleAutoAcceptCommand_TogglesSessionAutoAccept()
    {
        // Arrange
        await InitializeSutAsync(preference: BuildPreference(autoAccept: false));

        // Act
        _sut.ToggleAutoAcceptCommand.Execute(null);

        // Assert
        _sut.SessionAutoAccept.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleAutoAcceptCommand_WhenCalledTwice_ReturnsToOriginalValue()
    {
        // Arrange
        await InitializeSutAsync(preference: BuildPreference(autoAccept: false));

        // Act
        _sut.ToggleAutoAcceptCommand.Execute(null);
        _sut.ToggleAutoAcceptCommand.Execute(null);

        // Assert
        _sut.SessionAutoAccept.Should().BeFalse();
    }

    // ─── InsertSubagentCommand ───────────────────────────────────────────────

    [Fact]
    public async Task InsertSubagentCommand_CallsShowSubagentPickerAsync()
    {
        // Arrange
        await InitializeSutAsync();

        // Act
        await _sut.InsertSubagentCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowSubagentPickerAsync(
            Arg.Any<Action<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InsertSubagentCommand_WhenAgentSelected_InsertsTokenIntoMessageText()
    {
        // Arrange
        await InitializeSutAsync();
        _popupService.ShowSubagentPickerAsync(Arg.Any<Action<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callInfo.Arg<Action<string>>()("subagent-name");
                return Task.CompletedTask;
            });

        // Act
        await _sut.InsertSubagentCommand.ExecuteAsync(null);

        // Assert
        _sut.MessageText.Should().Be("@subagent-name");
    }

    // ─── InsertCommandCommand ────────────────────────────────────────────────

    [Fact]
    public async Task InsertCommandCommand_CallsShowCommandPaletteAsyncWithCallback()
    {
        // Arrange
        await InitializeSutAsync();

        // Act
        await _sut.InsertCommandCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowCommandPaletteAsync(
            Arg.Any<Action<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InsertCommandCommand_WhenCommandSelected_InsertsSlashToken()
    {
        // Arrange
        await InitializeSutAsync();
        _popupService.ShowCommandPaletteAsync(Arg.Any<Action<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callInfo.Arg<Action<string>>()("test");
                return Task.CompletedTask;
            });

        // Act
        await _sut.InsertCommandCommand.ExecuteAsync(null);

        // Assert
        _sut.MessageText.Should().Be("/test");
    }

    // ─── InsertFileCommand ───────────────────────────────────────────────────

    [Fact]
    public async Task InsertFileCommand_CallsShowFilePickerAsync()
    {
        // Arrange
        await InitializeSutAsync();

        // Act
        await _sut.InsertFileCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowFilePickerAsync(
            Arg.Any<Action<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InsertFileCommand_WhenFileSelected_InsertsAtPathToken()
    {
        // Arrange
        await InitializeSutAsync();
        _popupService.ShowFilePickerAsync(Arg.Any<Action<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callInfo.Arg<Action<string>>()("src/foo.cs");
                return Task.CompletedTask;
            });

        // Act
        await _sut.InsertFileCommand.ExecuteAsync(null);

        // Assert
        _sut.MessageText.Should().Be("@src/foo.cs");
    }

    // ─── InsertToken — Leading Space Behaviour ───────────────────────────────

    [Fact]
    public async Task InsertToken_WhenMessageTextIsEmpty_SetsTokenDirectly()
    {
        // Arrange
        await InitializeSutAsync();
        _popupService.ShowSubagentPickerAsync(Arg.Any<Action<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callInfo.Arg<Action<string>>()("agent");
                return Task.CompletedTask;
            });

        // Act
        await _sut.InsertSubagentCommand.ExecuteAsync(null);

        // Assert
        _sut.MessageText.Should().Be("@agent");
    }

    [Fact]
    public async Task InsertToken_WhenMessageTextIsNonEmpty_AppendsWithLeadingSpace()
    {
        // Arrange
        await InitializeSutAsync();
        _sut.MessageText = "Hello";
        _popupService.ShowSubagentPickerAsync(Arg.Any<Action<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callInfo.Arg<Action<string>>()("agent");
                return Task.CompletedTask;
            });

        // Act
        await _sut.InsertSubagentCommand.ExecuteAsync(null);

        // Assert
        _sut.MessageText.Should().Be("Hello @agent");
    }

    // ─── SendCommand — CanExecute ────────────────────────────────────────────

    [Fact]
    public void SendCommand_WhenMessageTextIsEmpty_CannotExecute()
    {
        // Arrange
        _sut.MessageText = string.Empty;

        // Assert
        _sut.SendCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void SendCommand_WhenMessageTextIsWhitespace_CannotExecute()
    {
        // Arrange
        _sut.MessageText = "   ";

        // Assert
        _sut.SendCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task SendCommand_WhenIsStreamingIsTrue_CannotExecute()
    {
        // Arrange
        await InitializeSutAsync(isStreaming: true);
        _sut.MessageText = "Hello";

        // Assert
        _sut.SendCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task SendCommand_WhenMessageTextIsNonEmptyAndNotStreaming_CanExecute()
    {
        // Arrange
        await InitializeSutAsync(isStreaming: false);
        _sut.MessageText = "Hello";

        // Assert
        _sut.SendCommand.CanExecute(null).Should().BeTrue();
    }

    // ─── SendCommand — Execution ─────────────────────────────────────────────

    [Fact]
    public async Task SendCommand_WhenExecuted_SendsMessageComposedMessage()
    {
        // Arrange
        await InitializeSutAsync();
        _sut.MessageText = "Hello world";

        MessageComposedMessage? capturedMessage = null;
        WeakReferenceMessenger.Default.Register<MessageComposedMessage>(this, (_, m) => capturedMessage = m);

        // Act
        await _sut.SendCommand.ExecuteAsync(null);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.Text.Should().Be("Hello world");
        capturedMessage.ProjectId.Should().Be("proj-1");
        capturedMessage.SessionId.Should().Be("sess-1");

        // Cleanup
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    [Fact]
    public async Task SendCommand_WhenExecuted_ClearsDraft()
    {
        // Arrange
        await InitializeSutAsync();
        _sut.MessageText = "Hello";

        // Act
        await _sut.SendCommand.ExecuteAsync(null);

        // Assert
        _draftService.Received(1).ClearDraft("sess-1");
    }

    [Fact]
    public async Task SendCommand_WhenExecuted_ClearsMessageText()
    {
        // Arrange
        await InitializeSutAsync();
        _sut.MessageText = "Hello";

        // Act
        await _sut.SendCommand.ExecuteAsync(null);

        // Assert
        _sut.MessageText.Should().BeEmpty();
    }

    [Fact]
    public async Task SendCommand_WhenExecuted_PopsPopup()
    {
        // Arrange
        await InitializeSutAsync();
        _sut.MessageText = "Hello";

        // Act
        await _sut.SendCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendCommand_IncludesSessionOverridesInMessage()
    {
        // Arrange
        await InitializeSutAsync(preference: BuildPreference(
            agentName: "custom-agent",
            thinkingLevel: ThinkingLevel.High,
            autoAccept: true));
        _sut.MessageText = "Hello";

        MessageComposedMessage? capturedMessage = null;
        WeakReferenceMessenger.Default.Register<MessageComposedMessage>(this, (_, m) => capturedMessage = m);

        // Act
        await _sut.SendCommand.ExecuteAsync(null);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.AgentOverride.Should().Be("custom-agent");
        capturedMessage.ThinkingLevelOverride.Should().Be(ThinkingLevel.High);
        capturedMessage.AutoAcceptOverride.Should().BeTrue();

        // Cleanup
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    // ─── CloseCommand ────────────────────────────────────────────────────────

    [Fact]
    public async Task CloseCommand_WhenMessageTextIsNonEmpty_SavesDraft()
    {
        // Arrange
        await InitializeSutAsync();
        _sut.MessageText = "Draft text";

        // Act
        await _sut.CloseCommand.ExecuteAsync(null);

        // Assert
        _draftService.Received(1).SaveDraft("sess-1", "Draft text");
    }

    [Fact]
    public async Task CloseCommand_WhenMessageTextIsEmpty_DoesNotSaveDraft()
    {
        // Arrange
        await InitializeSutAsync();
        _sut.MessageText = string.Empty;

        // Act
        await _sut.CloseCommand.ExecuteAsync(null);

        // Assert
        _draftService.DidNotReceive().SaveDraft(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task CloseCommand_PopsPopup()
    {
        // Arrange
        await InitializeSutAsync();

        // Act
        await _sut.CloseCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
    }

    // ─── SendButtonText ──────────────────────────────────────────────────────

    [Fact]
    public void SendButtonText_WhenNotStreaming_ReturnsInvia()
    {
        // Assert
        _sut.SendButtonText.Should().Be("Invia");
    }

    [Fact]
    public async Task SendButtonText_WhenStreaming_ReturnsAttendiRisposta()
    {
        // Arrange
        await InitializeSutAsync(isStreaming: true);

        // Assert
        _sut.SendButtonText.Should().Be("Attendi risposta…");
    }

    // ─── StreamingStateChangedMessage ────────────────────────────────────────

    [Fact]
    public async Task StreamingStateChangedMessage_WhenReceived_UpdatesIsStreaming()
    {
        // Arrange
        await InitializeSutAsync(isStreaming: false);

        // Act
        WeakReferenceMessenger.Default.Send(new StreamingStateChangedMessage(true));

        // Assert
        _sut.IsStreaming.Should().BeTrue();
    }

    [Fact]
    public async Task StreamingStateChangedMessage_WhenStreamingStops_UpdatesIsStreamingToFalse()
    {
        // Arrange
        await InitializeSutAsync(isStreaming: true);

        // Act
        WeakReferenceMessenger.Default.Send(new StreamingStateChangedMessage(false));

        // Assert
        _sut.IsStreaming.Should().BeFalse();
    }

    // ─── Dispose ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dispose_WhenMessageTextIsNonEmpty_SavesDraft()
    {
        // Arrange
        await InitializeSutAsync();
        _sut.MessageText = "Unsaved draft";

        // Act
        _sut.Dispose();

        // Assert
        _draftService.Received(1).SaveDraft("sess-1", "Unsaved draft");
    }

    [Fact]
    public async Task Dispose_WhenMessageTextIsEmpty_DoesNotSaveDraft()
    {
        // Arrange
        await InitializeSutAsync();
        _sut.MessageText = string.Empty;

        // Act
        _sut.Dispose();

        // Assert
        _draftService.DidNotReceive().SaveDraft(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Dispose_UnregistersFromMessenger()
    {
        // Arrange
        await InitializeSutAsync(isStreaming: false);

        // Act
        _sut.Dispose();

        // Send a message after dispose — should not update IsStreaming
        WeakReferenceMessenger.Default.Send(new StreamingStateChangedMessage(true));

        // Assert
        _sut.IsStreaming.Should().BeFalse();
    }
}
