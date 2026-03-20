using CommunityToolkit.Mvvm.Messaging;
using NSubstitute.ExceptionExtensions;
using openMob.Core.Data.Entities;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Messages;
using openMob.Core.Models;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="ContextSheetViewModel"/>.
/// Covers initialization, auto-save on property change, WeakReferenceMessenger publishing,
/// and computed display properties.
/// </summary>
public sealed class ContextSheetViewModelTests : IDisposable
{
    private readonly IProjectService _projectService;
    private readonly IProjectPreferenceService _preferenceService;
    private readonly IAppPopupService _popupService;
    private readonly IAgentService _agentService;
    private readonly ContextSheetViewModel _sut;

    public ContextSheetViewModelTests()
    {
        _projectService = Substitute.For<IProjectService>();
        _preferenceService = Substitute.For<IProjectPreferenceService>();
        _popupService = Substitute.For<IAppPopupService>();
        _agentService = Substitute.For<IAgentService>();

        // Default: return empty subagent list so InvokeSubagentCommand.CanExecute = false
        _agentService.GetSubagentAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AgentDto>());

        _sut = new ContextSheetViewModel(
            _projectService,
            _preferenceService,
            _popupService,
            _agentService);
    }

    public void Dispose()
    {
        // Unregister the test class instance (this) from the messenger.
        // Some tests subscribe 'this' as a listener to verify published messages;
        // this call cleans up those registrations to avoid cross-test pollution.
        // Note: the SUT (ContextSheetViewModel) manages its own messenger registrations independently.
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static ProjectDto BuildProject(string id = "proj-1", string worktree = "/home/user/myproject")
    {
        var time = new ProjectTimeDto(Created: 1710000000000, Initialized: null);
        return new ProjectDto(Id: id, Worktree: worktree, VcsDir: null, Vcs: "git", Time: time);
    }

    private static ProjectPreference BuildPreference(
        string projectId = "proj-1",
        string? agentName = null,
        string? defaultModelId = null,
        ThinkingLevel thinkingLevel = ThinkingLevel.Medium,
        bool autoAccept = false)
        => new()
        {
            ProjectId = projectId,
            AgentName = agentName,
            DefaultModelId = defaultModelId,
            ThinkingLevel = thinkingLevel,
            AutoAccept = autoAccept,
        };

    private static AgentDto BuildSubagentDto(string name = "coder")
        => new AgentDto(
            Name: name,
            Description: $"{name} subagent",
            Mode: "subagent",
            BuiltIn: false,
            TopP: null,
            Temperature: null,
            Color: null,
            Model: null,
            Prompt: null,
            Tools: default,
            Options: default,
            MaxSteps: null,
            Permission: default,
            Hidden: false);

    /// <summary>
    /// Configures the preference service to return a default preference for "proj-1"
    /// and calls InitializeAsync so that _currentProjectId is set and auto-save is enabled.
    /// </summary>
    private async Task InitializeWithDefaultsAsync(string projectId = "proj-1")
    {
        var project = BuildProject(projectId);
        _projectService.GetProjectByIdAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(project);
        _preferenceService.GetOrDefaultAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(BuildPreference(projectId));

        await _sut.InitializeAsync(projectId, "sess-1");
    }

    // ─── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Assert
        _sut.ProjectName.Should().Be("No project");
        _sut.SelectedAgentName.Should().BeNull();
        _sut.SelectedAgentDisplayName.Should().Be("build");
        _sut.SelectedModelId.Should().BeNull();
        _sut.SelectedModelDisplayName.Should().Be("No model");
        _sut.ThinkingLevel.Should().Be(ThinkingLevel.Medium);
        _sut.AutoAccept.Should().BeFalse();
        _sut.IsBusy.Should().BeFalse();
        _sut.ErrorMessage.Should().BeNull();
    }

    // ─── InitializeAsync — project name ───────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_WhenProjectExists_SetsProjectName()
    {
        // Arrange
        var project = BuildProject("proj-1", "/home/user/myproject");
        _projectService.GetProjectByIdAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(project);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference());

        // Act
        await _sut.InitializeAsync("proj-1", "sess-1");

        // Assert
        _sut.ProjectName.Should().Be("myproject");
    }

    [Fact]
    public async Task InitializeAsync_WhenProjectNotFound_SetsDefaultProjectName()
    {
        // Arrange
        _projectService.GetProjectByIdAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference());

        // Act
        await _sut.InitializeAsync("proj-1", "sess-1");

        // Assert
        _sut.ProjectName.Should().Be("No project");
    }

    // ─── InitializeAsync — preference loading ─────────────────────────────────

    [Fact]
    public async Task InitializeAsync_WhenPreferenceExists_PopulatesAllProperties()
    {
        // Arrange
        var project = BuildProject("proj-1");
        _projectService.GetProjectByIdAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(project);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference(
                agentName: "my-agent",
                defaultModelId: "anthropic/claude-sonnet-4-5",
                thinkingLevel: ThinkingLevel.High,
                autoAccept: true));

        // Act
        await _sut.InitializeAsync("proj-1", "sess-1");

        // Assert
        _sut.SelectedAgentName.Should().Be("my-agent");
        _sut.SelectedModelId.Should().Be("anthropic/claude-sonnet-4-5");
        _sut.ThinkingLevel.Should().Be(ThinkingLevel.High);
        _sut.AutoAccept.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WhenNoPreference_UsesDefaults()
    {
        // Arrange
        var project = BuildProject("proj-1");
        _projectService.GetProjectByIdAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(project);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference(agentName: null, defaultModelId: null, thinkingLevel: ThinkingLevel.Medium, autoAccept: false));

        // Act
        await _sut.InitializeAsync("proj-1", "sess-1");

        // Assert
        _sut.SelectedAgentName.Should().BeNull();
        _sut.SelectedModelId.Should().BeNull();
        _sut.ThinkingLevel.Should().Be(ThinkingLevel.Medium);
        _sut.AutoAccept.Should().BeFalse();
    }

    // ─── InitializeAsync — busy state ─────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_SetsBusyDuringLoad_IsFalseAfterCompletion()
    {
        // Arrange
        _projectService.GetProjectByIdAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference());

        // Act
        await _sut.InitializeAsync("proj-1", "sess-1");

        // Assert
        _sut.IsBusy.Should().BeFalse();
    }

    // ─── InitializeAsync — error handling ─────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_WhenServiceThrows_DoesNotCrash()
    {
        // Arrange
        _projectService.GetProjectByIdAsync("proj-1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var act = async () => await _sut.InitializeAsync("proj-1", "sess-1");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InitializeAsync_WhenServiceThrows_IsBusyIsFalse()
    {
        // Arrange
        _projectService.GetProjectByIdAsync("proj-1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        await _sut.InitializeAsync("proj-1", "sess-1");

        // Assert
        _sut.IsBusy.Should().BeFalse();
    }

    // ─── InitializeAsync — does not trigger auto-save ─────────────────────────

    [Fact]
    public async Task InitializeAsync_DoesNotTriggerAutoSave()
    {
        // Arrange
        var project = BuildProject("proj-1");
        _projectService.GetProjectByIdAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(project);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference(
                agentName: "my-agent",
                defaultModelId: "anthropic/claude-sonnet-4-5",
                thinkingLevel: ThinkingLevel.High,
                autoAccept: true));

        // Act
        await _sut.InitializeAsync("proj-1", "sess-1");

        // Assert — no Set* methods should have been called during initialization
        await _preferenceService.DidNotReceive().SetAgentAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _preferenceService.DidNotReceive().SetDefaultModelAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _preferenceService.DidNotReceive().SetThinkingLevelAsync(
            Arg.Any<string>(), Arg.Any<ThinkingLevel>(), Arg.Any<CancellationToken>());
        await _preferenceService.DidNotReceive().SetAutoAcceptAsync(
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    // ─── Auto-save — SelectedAgentName ────────────────────────────────────────

    [Fact]
    public async Task SelectedAgentName_WhenChangedAfterInit_CallsSetAgentAsync()
    {
        // Arrange
        await InitializeWithDefaultsAsync();
        _preferenceService.SetAgentAsync("proj-1", "new-agent", Arg.Any<CancellationToken>())
            .Returns(true);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference(agentName: "new-agent"));

        // Act
        _sut.SelectedAgentName = "new-agent";
        await Task.Delay(200); // allow fire-and-forget to complete

        // Assert
        await _preferenceService.Received(1).SetAgentAsync(
            "proj-1",
            "new-agent",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectedModelId_WhenChangedAfterInit_CallsSetDefaultModelAsync()
    {
        // Arrange
        await InitializeWithDefaultsAsync();
        _preferenceService.SetDefaultModelAsync("proj-1", "openai/gpt-4o", Arg.Any<CancellationToken>())
            .Returns(true);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference(defaultModelId: "openai/gpt-4o"));

        // Act
        _sut.SelectedModelId = "openai/gpt-4o";
        await Task.Delay(200); // allow fire-and-forget to complete

        // Assert
        await _preferenceService.Received(1).SetDefaultModelAsync(
            "proj-1",
            "openai/gpt-4o",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectedModelId_WhenSetToNull_CallsClearDefaultModelAsync()
    {
        // Arrange — initialize with a model already selected so the null change fires auto-save
        var project = BuildProject("proj-1");
        _projectService.GetProjectByIdAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(project);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference(defaultModelId: "anthropic/claude-sonnet-4-5"));
        await _sut.InitializeAsync("proj-1", "sess-1");

        _preferenceService.ClearDefaultModelAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(true);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference(defaultModelId: null));

        // Act
        _sut.SelectedModelId = null;
        await Task.Delay(200); // allow fire-and-forget to complete

        // Assert
        await _preferenceService.Received(1).ClearDefaultModelAsync(
            "proj-1",
            Arg.Any<CancellationToken>());
        await _preferenceService.DidNotReceive().SetDefaultModelAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectedModelId_WhenSetToNullAndSaveSucceeds_PublishesProjectPreferenceChangedMessage()
    {
        // Arrange
        var project = BuildProject("proj-1");
        _projectService.GetProjectByIdAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(project);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference(defaultModelId: "anthropic/claude-sonnet-4-5"));
        await _sut.InitializeAsync("proj-1", "sess-1");

        _preferenceService.ClearDefaultModelAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(true);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference(defaultModelId: null));

        ProjectPreferenceChangedMessage? receivedMessage = null;
        WeakReferenceMessenger.Default.Register<ProjectPreferenceChangedMessage>(
            this,
            (_, msg) => receivedMessage = msg);

        // Act
        _sut.SelectedModelId = null;
        await Task.Delay(200); // allow fire-and-forget to complete

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.ProjectId.Should().Be("proj-1");
    }

    [Fact]
    public async Task SelectedModelId_WhenSetToNullAndSaveFails_SetsErrorMessage()
    {
        // Arrange
        var project = BuildProject("proj-1");
        _projectService.GetProjectByIdAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(project);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference(defaultModelId: "anthropic/claude-sonnet-4-5"));
        await _sut.InitializeAsync("proj-1", "sess-1");

        _preferenceService.ClearDefaultModelAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        _sut.SelectedModelId = null;
        await Task.Delay(200); // allow fire-and-forget to complete

        // Assert
        _sut.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task ThinkingLevel_WhenChangedAfterInit_CallsSetThinkingLevelAsync()
    {
        // Arrange
        await InitializeWithDefaultsAsync();
        _preferenceService.SetThinkingLevelAsync("proj-1", ThinkingLevel.High, Arg.Any<CancellationToken>())
            .Returns(true);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference(thinkingLevel: ThinkingLevel.High));

        // Act
        _sut.ThinkingLevel = ThinkingLevel.High;
        await Task.Delay(200); // allow fire-and-forget to complete

        // Assert
        await _preferenceService.Received(1).SetThinkingLevelAsync(
            "proj-1",
            ThinkingLevel.High,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AutoAccept_WhenChangedAfterInit_CallsSetAutoAcceptAsync()
    {
        // Arrange
        await InitializeWithDefaultsAsync();
        _preferenceService.SetAutoAcceptAsync("proj-1", true, Arg.Any<CancellationToken>())
            .Returns(true);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference(autoAccept: true));

        // Act
        _sut.AutoAccept = true;
        await Task.Delay(200); // allow fire-and-forget to complete

        // Assert
        await _preferenceService.Received(1).SetAutoAcceptAsync(
            "proj-1",
            true,
            Arg.Any<CancellationToken>());
    }

    // ─── Messaging — WeakReferenceMessenger ───────────────────────────────────

    [Fact]
    public async Task SelectedAgentName_WhenSaveSucceeds_PublishesProjectPreferenceChangedMessage()
    {
        // Arrange
        await InitializeWithDefaultsAsync();
        _preferenceService.SetAgentAsync("proj-1", "new-agent", Arg.Any<CancellationToken>())
            .Returns(true);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference(projectId: "proj-1", agentName: "new-agent"));

        ProjectPreferenceChangedMessage? receivedMessage = null;
        WeakReferenceMessenger.Default.Register<ProjectPreferenceChangedMessage>(
            this,
            (_, msg) => receivedMessage = msg);

        // Act
        _sut.SelectedAgentName = "new-agent";
        await Task.Delay(200); // allow fire-and-forget to complete

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.ProjectId.Should().Be("proj-1");
    }

    [Fact]
    public async Task SelectedAgentName_WhenSaveFails_SetsErrorMessage()
    {
        // Arrange
        await InitializeWithDefaultsAsync();
        _preferenceService.SetAgentAsync("proj-1", "bad-agent", Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        _sut.SelectedAgentName = "bad-agent";
        await Task.Delay(200); // allow fire-and-forget to complete

        // Assert
        _sut.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task SelectedAgentName_WhenSaveFails_PropertyValueIsNotRolledBack()
    {
        // Arrange
        await InitializeWithDefaultsAsync();
        _preferenceService.SetAgentAsync("proj-1", "bad-agent", Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        _sut.SelectedAgentName = "bad-agent";
        await Task.Delay(200); // allow fire-and-forget to complete

        // Assert — the UI value is NOT rolled back on failure
        _sut.SelectedAgentName.Should().Be("bad-agent");
    }

    // ─── Computed properties — SelectedAgentDisplayName ───────────────────────

    [Fact]
    public void SelectedAgentDisplayName_WhenAgentNameIsNull_ReturnsBuild()
    {
        // Arrange
        _sut.SelectedAgentName = null;

        // Act
        var result = _sut.SelectedAgentDisplayName;

        // Assert
        result.Should().Be("build");
    }

    [Fact]
    public void SelectedAgentDisplayName_WhenAgentNameIsSet_ReturnsAgentName()
    {
        // Arrange
        _sut.SelectedAgentName = "my-agent";

        // Act
        var result = _sut.SelectedAgentDisplayName;

        // Assert
        result.Should().Be("my-agent");
    }

    // ─── Computed properties — SelectedModelDisplayName ───────────────────────

    [Fact]
    public void SelectedModelDisplayName_WhenModelIdIsNull_ReturnsNoModel()
    {
        // Arrange
        _sut.SelectedModelId = null;

        // Act
        var result = _sut.SelectedModelDisplayName;

        // Assert
        result.Should().Be("No model");
    }

    [Fact]
    public void SelectedModelDisplayName_WhenModelIdIsSet_ReturnsExtractedName()
    {
        // Arrange
        _sut.SelectedModelId = "anthropic/claude-sonnet-4-5";

        // Act
        var result = _sut.SelectedModelDisplayName;

        // Assert
        result.Should().Be("claude-sonnet-4-5");
    }

    // ─── Commands — SelectModelCommand ────────────────────────────────────────

    [Fact]
    public async Task SelectModelCommand_WhenExecuted_CallsShowModelPickerAsync()
    {
        // Arrange
        _popupService.ShowModelPickerAsync(Arg.Any<Action<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SelectModelCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowModelPickerAsync(
            Arg.Any<Action<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectModelCommand_WhenModelSelected_UpdatesSelectedModelId()
    {
        // Arrange
        const string selectedModelId = "anthropic/claude-opus-4-5";

        _popupService.ShowModelPickerAsync(Arg.Any<Action<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                // Invoke the callback synchronously to simulate the user picking a model
                var callback = callInfo.Arg<Action<string>>();
                callback(selectedModelId);
                return Task.CompletedTask;
            });

        // Act
        await _sut.SelectModelCommand.ExecuteAsync(null);

        // Assert
        _sut.SelectedModelId.Should().Be(selectedModelId);
    }

    // ─── Commands — ChangeThinkingLevelCommand ────────────────────────────────

    [Theory]
    [InlineData(ThinkingLevel.Low)]
    [InlineData(ThinkingLevel.Medium)]
    [InlineData(ThinkingLevel.High)]
    public void ChangeThinkingLevelCommand_WhenCalled_UpdatesThinkingLevel(ThinkingLevel level)
    {
        // Act
        _sut.ChangeThinkingLevelCommand.Execute(level);

        // Assert
        _sut.ThinkingLevel.Should().Be(level);
    }

    // ─── Commands — ToggleAutoAcceptCommand ───────────────────────────────────

    [Fact]
    public async Task ToggleAutoAcceptCommand_WhenAutoAcceptIsFalse_TogglesItToTrue()
    {
        // Arrange — initialize with autoAccept = false (default)
        await InitializeWithDefaultsAsync();
        _preferenceService.SetAutoAcceptAsync("proj-1", true, Arg.Any<CancellationToken>())
            .Returns(true);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference(autoAccept: true));

        // Act
        await _sut.ToggleAutoAcceptCommand.ExecuteAsync(null);

        // Assert
        _sut.AutoAccept.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleAutoAcceptCommand_WhenAutoAcceptIsTrue_TogglesItToFalse()
    {
        // Arrange — initialize with autoAccept = true
        var project = BuildProject("proj-1");
        _projectService.GetProjectByIdAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(project);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference(autoAccept: true));
        await _sut.InitializeAsync("proj-1", "sess-1");

        _preferenceService.SetAutoAcceptAsync("proj-1", false, Arg.Any<CancellationToken>())
            .Returns(true);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference(autoAccept: false));

        // Act
        await _sut.ToggleAutoAcceptCommand.ExecuteAsync(null);

        // Assert
        _sut.AutoAccept.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleAutoAcceptCommand_WhenSaveSucceeds_PublishesProjectPreferenceChangedMessage()
    {
        // Arrange
        await InitializeWithDefaultsAsync();
        _preferenceService.SetAutoAcceptAsync("proj-1", true, Arg.Any<CancellationToken>())
            .Returns(true);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference(autoAccept: true));

        ProjectPreferenceChangedMessage? receivedMessage = null;
        WeakReferenceMessenger.Default.Register<ProjectPreferenceChangedMessage>(
            this,
            (_, msg) => receivedMessage = msg);

        // Act
        await _sut.ToggleAutoAcceptCommand.ExecuteAsync(null);

        // Assert
        receivedMessage.Should().NotBeNull();
        receivedMessage!.ProjectId.Should().Be("proj-1");
    }

    [Fact]
    public async Task ToggleAutoAcceptCommand_WhenSaveSucceeds_CallsSetAutoAcceptAsyncWithNewValue()
    {
        // Arrange — initial AutoAccept = false, so new value = true
        await InitializeWithDefaultsAsync();
        _preferenceService.SetAutoAcceptAsync("proj-1", true, Arg.Any<CancellationToken>())
            .Returns(true);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference(autoAccept: true));

        // Act
        await _sut.ToggleAutoAcceptCommand.ExecuteAsync(null);

        // Assert
        await _preferenceService.Received(1).SetAutoAcceptAsync(
            "proj-1",
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleAutoAcceptCommand_WhenSaveFails_RevertsAutoAcceptToPreviousValue()
    {
        // Arrange — initial AutoAccept = false
        await InitializeWithDefaultsAsync();
        _preferenceService.SetAutoAcceptAsync("proj-1", true, Arg.Any<CancellationToken>())
            .Returns(false); // save fails

        // Act
        await _sut.ToggleAutoAcceptCommand.ExecuteAsync(null);

        // Assert — reverted to original false
        _sut.AutoAccept.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleAutoAcceptCommand_WhenSaveFails_SetsErrorMessage()
    {
        // Arrange
        await InitializeWithDefaultsAsync();
        _preferenceService.SetAutoAcceptAsync("proj-1", true, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.ToggleAutoAcceptCommand.ExecuteAsync(null);

        // Assert
        _sut.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task ToggleAutoAcceptCommand_WhenSaveFails_DoesNotPublishProjectPreferenceChangedMessage()
    {
        // Arrange
        await InitializeWithDefaultsAsync();
        _preferenceService.SetAutoAcceptAsync("proj-1", true, Arg.Any<CancellationToken>())
            .Returns(false);

        var messagePublished = false;
        WeakReferenceMessenger.Default.Register<ProjectPreferenceChangedMessage>(
            this,
            (_, _) => messagePublished = true);

        // Act
        await _sut.ToggleAutoAcceptCommand.ExecuteAsync(null);

        // Assert
        messagePublished.Should().BeFalse();
    }

    // ─── Commands — InvokeSubagentCommand ─────────────────────────────────────

    [Fact]
    public async Task InvokeSubagentCommand_WhenSubagentListIsEmpty_CannotExecute()
    {
        // Arrange — default setup returns empty subagent list
        await InitializeWithDefaultsAsync();

        // Assert
        _sut.InvokeSubagentCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task InvokeSubagentCommand_WhenSubagentListIsNotEmpty_CanExecute()
    {
        // Arrange — initialize with subagents available
        var project = BuildProject("proj-1");
        _projectService.GetProjectByIdAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(project);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference());
        _agentService.GetSubagentAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AgentDto>
            {
                new AgentDto(
                    Name: "coder",
                    Description: "Coder subagent",
                    Mode: "subagent",
                    BuiltIn: false,
                    TopP: null,
                    Temperature: null,
                    Color: null,
                    Model: null,
                    Prompt: null,
                    Tools: default,
                    Options: default,
                    MaxSteps: null,
                    Permission: default,
                    Hidden: false),
            });

        await _sut.InitializeAsync("proj-1", "sess-1");

        // Assert
        _sut.InvokeSubagentCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeSubagentCommand_WhenSubagentsAvailable_CallsShowSubagentPickerAsync()
    {
        // Arrange — initialize with subagents available so CanExecute = true
        var subagents = new List<AgentDto>
        {
            new AgentDto(
                Name: "sub-agent",
                Description: "Sub Agent",
                Mode: "subagent",
                BuiltIn: false,
                TopP: null,
                Temperature: null,
                Color: null,
                Model: null,
                Prompt: null,
                Tools: default,
                Options: default,
                MaxSteps: null,
                Permission: default,
                Hidden: false),
        };
        _agentService.GetSubagentAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(subagents);
        _popupService.ShowSubagentPickerAsync(Arg.Any<Action<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await InitializeWithDefaultsAsync();

        // Assert — [m-003]: ErrorMessage is null before execution (clean initial state)
        _sut.ErrorMessage.Should().BeNull();

        // Act
        await _sut.InvokeSubagentCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowSubagentPickerAsync(
            Arg.Any<Action<string>>(),
            Arg.Any<CancellationToken>());

        // Assert — [m-003]: IsBusy is false after successful completion
        _sut.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeSubagentCommand_WhenSubagentSelected_SetsNotSupportedErrorMessage()
    {
        // Arrange — initialize with subagents available so CanExecute = true
        _agentService.GetSubagentAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AgentDto> { BuildSubagentDto("coder") });

        // Simulate the popup invoking the callback synchronously with the selected agent name
        _popupService.ShowSubagentPickerAsync(Arg.Any<Action<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var callback = callInfo.Arg<Action<string>>();
                callback("coder");
                return Task.CompletedTask;
            });

        await InitializeWithDefaultsAsync();

        // Act
        await _sut.InvokeSubagentCommand.ExecuteAsync(null);

        // Assert
        _sut.ErrorMessage.Should().Contain("not supported");
    }

    [Fact]
    public async Task InvokeSubagentCommand_WhenPopupThrows_SetsErrorMessage()
    {
        // Arrange — initialize with subagents available so CanExecute = true
        _agentService.GetSubagentAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AgentDto> { BuildSubagentDto("coder") });

        _popupService.ShowSubagentPickerAsync(Arg.Any<Action<string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Popup failed"));

        await InitializeWithDefaultsAsync();

        // Act
        await _sut.InvokeSubagentCommand.ExecuteAsync(null);

        // Assert
        _sut.ErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeSubagentCommand_WhenPopupThrows_IsBusyIsFalse()
    {
        // Arrange — initialize with subagents available so CanExecute = true
        _agentService.GetSubagentAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AgentDto> { BuildSubagentDto("coder") });

        _popupService.ShowSubagentPickerAsync(Arg.Any<Action<string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Popup failed"));

        await InitializeWithDefaultsAsync();

        // Act
        await _sut.InvokeSubagentCommand.ExecuteAsync(null);

        // Assert
        _sut.IsBusy.Should().BeFalse();
    }

    // ─── InitializeAsync — subagent loading ───────────────────────────────────

    [Fact]
    public async Task InitializeAsync_LoadsSubagentListForCanExecuteEvaluation()
    {
        // Arrange — subagents available
        var project = BuildProject("proj-1");
        _projectService.GetProjectByIdAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(project);
        _preferenceService.GetOrDefaultAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(BuildPreference());
        _agentService.GetSubagentAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AgentDto> { BuildSubagentDto("coder") });

        // Act
        await _sut.InitializeAsync("proj-1", "sess-1");

        // Assert
        _sut.InvokeSubagentCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WhenNoSubagentsAvailable_InvokeSubagentCommandCannotExecute()
    {
        // Arrange — default: empty subagent list
        await InitializeWithDefaultsAsync();

        // Assert
        _sut.InvokeSubagentCommand.CanExecute(null).Should().BeFalse();
    }

    // ─── Commands — SelectAgentCommand ───────────────────────────────────────

    [Fact]
    public async Task SelectAgentCommand_WhenExecuted_CallsShowAgentPickerAsync()
    {
        // Arrange
        _popupService.ShowAgentPickerAsync(Arg.Any<Action<string?>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SelectAgentCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowAgentPickerAsync(
            Arg.Any<Action<string?>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SelectAgentCommand_WhenCallbackInvokedWithAgentName_UpdatesSelectedAgentName()
    {
        // Arrange
        _popupService.ShowAgentPickerAsync(Arg.Any<Action<string?>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                // Invoke the callback synchronously to simulate the user picking an agent
                var callback = callInfo.Arg<Action<string?>>();
                callback("coder");
                return Task.CompletedTask;
            });

        // Act
        await _sut.SelectAgentCommand.ExecuteAsync(null);

        // Assert
        _sut.SelectedAgentName.Should().Be("coder");
    }

    [Fact]
    public async Task SelectAgentCommand_WhenCallbackInvokedWithNull_SetsSelectedAgentNameToNull()
    {
        // Arrange
        _sut.SelectedAgentName = "previous-agent";
        _popupService.ShowAgentPickerAsync(Arg.Any<Action<string?>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var callback = callInfo.Arg<Action<string?>>();
                callback(null);
                return Task.CompletedTask;
            });

        // Act
        await _sut.SelectAgentCommand.ExecuteAsync(null);

        // Assert
        _sut.SelectedAgentName.Should().BeNull();
    }
}
