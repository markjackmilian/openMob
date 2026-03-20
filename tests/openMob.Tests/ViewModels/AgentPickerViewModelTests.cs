using NSubstitute.ExceptionExtensions;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="AgentPickerViewModel"/>.
/// </summary>
public sealed class AgentPickerViewModelTests
{
    private readonly IAgentService _agentService;
    private readonly IAppPopupService _popupService;
    private readonly AgentPickerViewModel _sut;

    public AgentPickerViewModelTests()
    {
        _agentService = Substitute.For<IAgentService>();
        _popupService = Substitute.For<IAppPopupService>();

        _sut = new AgentPickerViewModel(_agentService, _popupService);
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private static AgentDto BuildAgent(string name = "claude", string? description = "Claude AI")
    {
        return new AgentDto(
            Name: name, Description: description, Mode: "primary", BuiltIn: true,
            TopP: null, Temperature: null, Color: null, Model: null, Prompt: null,
            Tools: default, Options: default, MaxSteps: null, Permission: default,
            Hidden: false);
    }

    // ─── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesWithEmptyAgentsCollection()
    {
        // Assert
        _sut.Agents.Should().BeEmpty();
        _sut.IsLoading.Should().BeFalse();
        _sut.IsEmpty.Should().BeFalse();
        _sut.SelectedAgentName.Should().BeNull();
    }

    // ─── LoadAgentsCommand ────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAgentsCommand_WhenServiceReturnsAgents_PopulatesCollection()
    {
        // Arrange — primary mode (default): ViewModel calls GetPrimaryAgentsAsync
        var agents = new List<AgentDto>
        {
            BuildAgent("claude", "Claude AI"),
            BuildAgent("gpt", "GPT Agent"),
        };
        _agentService.GetPrimaryAgentsAsync(Arg.Any<CancellationToken>()).Returns(agents);

        // Act
        await _sut.LoadAgentsCommand.ExecuteAsync(null);

        // Assert — 2 real agents (no Default entry)
        _sut.Agents.Should().HaveCount(2);
        _sut.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAgentsCommand_WhenServiceReturnsEmpty_SetsIsEmptyTrue()
    {
        // Arrange — primary mode: even with no agents, Default entry is prepended (count = 1)
        // IsEmpty is based on Agents.Count == 0, which is false when Default is present.
        // The ViewModel sets IsEmpty = Agents.Count == 0, so with Default entry it is false.
        // We test the subagent mode for the truly-empty case.
        _sut.IsSubagentMode = true;
        _agentService.GetAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AgentDto>());

        // Act
        await _sut.LoadAgentsCommand.ExecuteAsync(null);

        // Assert
        _sut.Agents.Should().BeEmpty();
        _sut.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAgentsCommand_MapsAgentDtoToAgentItem()
    {
        // Arrange — primary mode: ViewModel calls GetPrimaryAgentsAsync
        var agents = new List<AgentDto> { BuildAgent("claude", "Claude AI") };
        _agentService.GetPrimaryAgentsAsync(Arg.Any<CancellationToken>()).Returns(agents);

        // Act
        await _sut.LoadAgentsCommand.ExecuteAsync(null);

        // Assert — collection contains the mapped agent (plus the Default entry at index 0)
        _sut.Agents.Should().Contain(a => a.Name == "claude" && a.Description == "Claude AI");
    }

    [Fact]
    public async Task LoadAgentsCommand_WhenSelectedAgentNameIsSet_MarksMatchingAgentAsSelected()
    {
        // Arrange — primary mode: ViewModel calls GetPrimaryAgentsAsync
        _sut.SelectedAgentName = "claude";
        var agents = new List<AgentDto>
        {
            BuildAgent("claude"),
            BuildAgent("gpt"),
        };
        _agentService.GetPrimaryAgentsAsync(Arg.Any<CancellationToken>()).Returns(agents);

        // Act
        await _sut.LoadAgentsCommand.ExecuteAsync(null);

        // Assert — claude is selected, gpt is not
        _sut.Agents.Should().Contain(a => a.Name == "claude" && a.IsSelected);
        _sut.Agents.Should().Contain(a => a.Name == "gpt" && !a.IsSelected);
    }

    [Fact]
    public async Task LoadAgentsCommand_SetsIsLoadingFalseAfterCompletion()
    {
        // Arrange — primary mode: ViewModel calls GetPrimaryAgentsAsync
        _agentService.GetPrimaryAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AgentDto>());

        // Act
        await _sut.LoadAgentsCommand.ExecuteAsync(null);

        // Assert
        _sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAgentsCommand_WhenServiceThrows_SetsIsEmptyTrue()
    {
        // Arrange — primary mode: ViewModel calls GetPrimaryAgentsAsync
        _agentService.GetPrimaryAgentsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Error"));

        // Act
        await _sut.LoadAgentsCommand.ExecuteAsync(null);

        // Assert — on exception, Agents is reset to empty and IsEmpty is true
        _sut.Agents.Should().BeEmpty();
        _sut.IsEmpty.Should().BeTrue();
        _sut.IsLoading.Should().BeFalse();
    }

    // ─── SelectAgentCommand ───────────────────────────────────────────────────

    [Fact]
    public async Task SelectAgentCommand_SetsSelectedAgentName()
    {
        // Arrange — primary mode: ViewModel calls GetPrimaryAgentsAsync
        var agents = new List<AgentDto> { BuildAgent("claude"), BuildAgent("gpt") };
        _agentService.GetPrimaryAgentsAsync(Arg.Any<CancellationToken>()).Returns(agents);
        await _sut.LoadAgentsCommand.ExecuteAsync(null);

        // Act
        await _sut.SelectAgentCommand.ExecuteAsync("claude");

        // Assert
        _sut.SelectedAgentName.Should().Be("claude");
    }

    [Fact]
    public async Task SelectAgentCommand_UpdatesIsSelectedInCollection()
    {
        // Arrange — primary mode: ViewModel calls GetPrimaryAgentsAsync
        var agents = new List<AgentDto> { BuildAgent("claude"), BuildAgent("gpt") };
        _agentService.GetPrimaryAgentsAsync(Arg.Any<CancellationToken>()).Returns(agents);
        await _sut.LoadAgentsCommand.ExecuteAsync(null);

        // Act
        await _sut.SelectAgentCommand.ExecuteAsync("gpt");

        // Assert — collection contains gpt (selected) and claude (not selected)
        _sut.Agents.Should().Contain(a => a.Name == "gpt" && a.IsSelected);
        _sut.Agents.Should().Contain(a => a.Name == "claude" && !a.IsSelected);
    }

    [Fact]
    public async Task SelectAgentCommand_ClosesPopup()
    {
        // Arrange — primary mode: ViewModel calls GetPrimaryAgentsAsync
        var agents = new List<AgentDto> { BuildAgent("claude") };
        _agentService.GetPrimaryAgentsAsync(Arg.Any<CancellationToken>()).Returns(agents);
        await _sut.LoadAgentsCommand.ExecuteAsync(null);

        // Act
        await _sut.SelectAgentCommand.ExecuteAsync("claude");

        // Assert
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
    }

    // ─── LoadAgentsCommand — subagent mode (no Default entry) ─────────────────

    [Fact]
    public async Task LoadAgentsCommand_WhenSubagentMode_DoesNotPrependDefaultEntry()
    {
        // Arrange
        _sut.IsSubagentMode = true;
        var agents = new List<AgentDto>
        {
            BuildAgent("coder"),
            BuildAgent("researcher"),
        };
        _agentService.GetAgentsAsync(Arg.Any<CancellationToken>()).Returns(agents);

        // Act
        await _sut.LoadAgentsCommand.ExecuteAsync(null);

        // Assert
        _sut.Agents.Should().HaveCount(2);
        _sut.Agents.Should().NotContain(a => a.Name == null);
    }

    [Fact]
    public async Task LoadAgentsCommand_WhenSubagentMode_CallsGetAgentsAsyncNotGetPrimaryAgentsAsync()
    {
        // Arrange
        _sut.IsSubagentMode = true;
        _agentService.GetAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AgentDto>());

        // Act
        await _sut.LoadAgentsCommand.ExecuteAsync(null);

        // Assert
        await _agentService.Received(1).GetAgentsAsync(Arg.Any<CancellationToken>());
        await _agentService.DidNotReceive().GetPrimaryAgentsAsync(Arg.Any<CancellationToken>());
    }

    // ─── SelectAgentCommand — primary mode callback ───────────────────────────

    [Fact]
    public async Task SelectAgentCommand_WhenPrimaryModeAndAgentSelected_InvokesCallback()
    {
        // Arrange
        _sut.IsSubagentMode = false;
        string? capturedAgentName = "not-set";
        _sut.OnAgentSelected = name => capturedAgentName = name;

        // Act
        await _sut.SelectAgentCommand.ExecuteAsync("coder");

        // Assert
        capturedAgentName.Should().Be("coder");
    }

    [Fact]
    public async Task SelectAgentCommand_WhenPrimaryMode_CallsPopPopupAsync()
    {
        // Arrange
        _sut.IsSubagentMode = false;
        _sut.OnAgentSelected = _ => { };

        // Act
        await _sut.SelectAgentCommand.ExecuteAsync("coder");

        // Assert
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
    }

    // ─── SelectAgentCommand — subagent mode ───────────────────────────────────

    [Fact]
    public async Task SelectAgentCommand_WhenSubagentMode_DoesNotInvokeCallback()
    {
        // Arrange
        _sut.IsSubagentMode = true;
        var callbackInvoked = false;
        _sut.OnAgentSelected = _ => callbackInvoked = true;

        // Act
        await _sut.SelectAgentCommand.ExecuteAsync("coder");

        // Assert
        callbackInvoked.Should().BeFalse();
    }

    [Fact]
    public async Task SelectAgentCommand_WhenSubagentMode_SetsSelectedSubagentName()
    {
        // Arrange
        _sut.IsSubagentMode = true;

        // Act
        await _sut.SelectAgentCommand.ExecuteAsync("coder");

        // Assert
        _sut.SelectedSubagentName.Should().Be("coder");
    }

    [Fact]
    public async Task SelectAgentCommand_WhenCallbackIsNull_DoesNotThrow()
    {
        // Arrange
        _sut.IsSubagentMode = false;
        _sut.OnAgentSelected = null;

        // Act
        var act = async () => await _sut.SelectAgentCommand.ExecuteAsync("coder");

        // Assert
        await act.Should().NotThrowAsync();
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
    }

    // ─── SheetTitle ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(false, "Select Agent")]
    [InlineData(true, "Invoke Subagent")]
    public void SheetTitle_ReturnsCorrectTitleBasedOnMode(bool isSubagentMode, string expectedTitle)
    {
        // Arrange
        _sut.IsSubagentMode = isSubagentMode;

        // Act
        var result = _sut.SheetTitle;

        // Assert
        result.Should().Be(expectedTitle);
    }
}
