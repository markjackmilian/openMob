using NSubstitute.ExceptionExtensions;
using openMob.Core.Data.Entities;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Models;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="ContextSheetViewModel"/>.
/// Covers context loading, thinking level changes, auto-accept toggling,
/// and popup service delegation.
/// </summary>
public sealed class ContextSheetViewModelTests
{
    private readonly IProjectService _projectService;
    private readonly IProviderService _providerService;
    private readonly IAppPopupService _popupService;
    private readonly IProjectPreferenceService _preferenceService;
    private readonly ContextSheetViewModel _sut;

    public ContextSheetViewModelTests()
    {
        _projectService = Substitute.For<IProjectService>();
        _providerService = Substitute.For<IProviderService>();
        _popupService = Substitute.For<IAppPopupService>();
        _preferenceService = Substitute.For<IProjectPreferenceService>();

        _sut = new ContextSheetViewModel(
            _projectService,
            _providerService,
            _popupService,
            _preferenceService);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static ProjectDto BuildProject(string id = "proj-1", string worktree = "/home/user/myproject")
    {
        var time = new ProjectTimeDto(Created: 1710000000000, Initialized: null);
        return new ProjectDto(Id: id, Worktree: worktree, VcsDir: null, Vcs: "git", Time: time);
    }

    // ─── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Assert
        _sut.ProjectName.Should().Be("No project");
        _sut.ModelName.Should().Be("No model");
        _sut.AgentName.Should().Be("Default");
        _sut.ThinkingLevel.Should().Be(ThinkingLevel.Medium);
        _sut.AutoAccept.Should().BeFalse();
        _sut.CurrentProjectId.Should().BeNull();
    }

    // ─── LoadContextCommand ──────────────────────────────────────────────────

    [Fact]
    public async Task LoadContextCommand_WhenProjectExists_SetsProjectName()
    {
        // Arrange
        var project = BuildProject("proj-1", "/home/user/myproject");
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>()).Returns(project);

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.ProjectName.Should().Be("myproject");
        _sut.CurrentProjectId.Should().Be("proj-1");
    }

    [Fact]
    public async Task LoadContextCommand_WhenNoProject_SetsProjectNameToDefault()
    {
        // Arrange
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns((ProjectDto?)null);

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.ProjectName.Should().Be("No project");
        _sut.CurrentProjectId.Should().BeNull();
    }

    [Fact]
    public async Task LoadContextCommand_WhenPreferenceHasModel_SetsModelName()
    {
        // Arrange
        var project = BuildProject("proj-1");
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>()).Returns(project);
        _preferenceService.GetAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(new ProjectPreference { ProjectId = "proj-1", DefaultModelId = "anthropic/claude-sonnet-4-5" });

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.ModelName.Should().Be("claude-sonnet-4-5");
    }

    [Fact]
    public async Task LoadContextCommand_WhenNoPreference_SetsModelNameToDefault()
    {
        // Arrange
        var project = BuildProject("proj-1");
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>()).Returns(project);
        _preferenceService.GetAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns((ProjectPreference?)null);

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.ModelName.Should().Be("No model");
    }

    [Fact]
    public async Task LoadContextCommand_WhenPreferenceModelIdIsNull_SetsModelNameToDefault()
    {
        // Arrange
        var project = BuildProject("proj-1");
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>()).Returns(project);
        _preferenceService.GetAsync("proj-1", Arg.Any<CancellationToken>())
            .Returns(new ProjectPreference { ProjectId = "proj-1", DefaultModelId = null });

        // Act
        await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert
        _sut.ModelName.Should().Be("No model");
    }

    [Fact]
    public async Task LoadContextCommand_WhenServiceThrows_DoesNotCrash()
    {
        // Arrange
        _projectService.GetCurrentProjectAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var act = async () => await _sut.LoadContextCommand.ExecuteAsync(null);

        // Assert — should not throw (error is caught internally)
        await act.Should().NotThrowAsync();
    }

    // ─── ChangeThinkingLevelCommand ──────────────────────────────────────────

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

    // ─── ToggleAutoAcceptCommand ─────────────────────────────────────────────

    [Fact]
    public void ToggleAutoAcceptCommand_WhenCalledOnce_SetsAutoAcceptTrue()
    {
        // Arrange — default is false

        // Act
        _sut.ToggleAutoAcceptCommand.Execute(null);

        // Assert
        _sut.AutoAccept.Should().BeTrue();
    }

    [Fact]
    public void ToggleAutoAcceptCommand_WhenCalledTwice_SetsAutoAcceptFalse()
    {
        // Act
        _sut.ToggleAutoAcceptCommand.Execute(null);
        _sut.ToggleAutoAcceptCommand.Execute(null);

        // Assert
        _sut.AutoAccept.Should().BeFalse();
    }

    // ─── OpenModelPickerCommand ──────────────────────────────────────────────

    [Fact]
    public async Task OpenModelPickerCommand_WhenExecuted_CallsPopupService()
    {
        // Arrange
        _popupService.ShowModelPickerAsync(Arg.Any<Action<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.OpenModelPickerCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowModelPickerAsync(
            Arg.Any<Action<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OpenModelPickerCommand_WhenModelSelected_UpdatesModelName()
    {
        // Arrange
        _popupService.ShowModelPickerAsync(Arg.Any<Action<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var callback = callInfo.Arg<Action<string>>();
                callback("openai/gpt-4o");
                return Task.CompletedTask;
            });

        // Act
        await _sut.OpenModelPickerCommand.ExecuteAsync(null);

        // Assert
        _sut.ModelName.Should().Be("gpt-4o");
    }

    // ─── InvokeSubagentCommand ───────────────────────────────────────────────

    [Fact]
    public async Task InvokeSubagentCommand_WhenExecuted_CallsShowAgentPickerSubagentMode()
    {
        // Arrange
        _popupService.ShowAgentPickerSubagentModeAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await _sut.InvokeSubagentCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowAgentPickerSubagentModeAsync(
            Arg.Any<CancellationToken>());
    }
}
