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
            Tools: default, Options: default, MaxSteps: null, Permission: default);
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
        // Arrange
        var agents = new List<AgentDto>
        {
            BuildAgent("claude", "Claude AI"),
            BuildAgent("gpt", "GPT Agent"),
        };
        _agentService.GetAgentsAsync(Arg.Any<CancellationToken>()).Returns(agents);

        // Act
        await _sut.LoadAgentsCommand.ExecuteAsync(null);

        // Assert
        _sut.Agents.Should().HaveCount(2);
        _sut.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAgentsCommand_WhenServiceReturnsEmpty_SetsIsEmptyTrue()
    {
        // Arrange
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
        // Arrange
        var agents = new List<AgentDto> { BuildAgent("claude", "Claude AI") };
        _agentService.GetAgentsAsync(Arg.Any<CancellationToken>()).Returns(agents);

        // Act
        await _sut.LoadAgentsCommand.ExecuteAsync(null);

        // Assert
        _sut.Agents.Should().ContainSingle(a => a.Name == "claude" && a.Description == "Claude AI");
    }

    [Fact]
    public async Task LoadAgentsCommand_WhenSelectedAgentNameIsSet_MarksMatchingAgentAsSelected()
    {
        // Arrange
        _sut.SelectedAgentName = "claude";
        var agents = new List<AgentDto>
        {
            BuildAgent("claude"),
            BuildAgent("gpt"),
        };
        _agentService.GetAgentsAsync(Arg.Any<CancellationToken>()).Returns(agents);

        // Act
        await _sut.LoadAgentsCommand.ExecuteAsync(null);

        // Assert
        _sut.Agents.Should().ContainSingle(a => a.Name == "claude" && a.IsSelected);
        _sut.Agents.Should().ContainSingle(a => a.Name == "gpt" && !a.IsSelected);
    }

    [Fact]
    public async Task LoadAgentsCommand_SetsIsLoadingFalseAfterCompletion()
    {
        // Arrange
        _agentService.GetAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<AgentDto>());

        // Act
        await _sut.LoadAgentsCommand.ExecuteAsync(null);

        // Assert
        _sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAgentsCommand_WhenServiceThrows_SetsIsEmptyTrue()
    {
        // Arrange
        _agentService.GetAgentsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Error"));

        // Act
        await _sut.LoadAgentsCommand.ExecuteAsync(null);

        // Assert
        _sut.Agents.Should().BeEmpty();
        _sut.IsEmpty.Should().BeTrue();
        _sut.IsLoading.Should().BeFalse();
    }

    // ─── SelectAgentCommand ───────────────────────────────────────────────────

    [Fact]
    public async Task SelectAgentCommand_SetsSelectedAgentName()
    {
        // Arrange
        var agents = new List<AgentDto> { BuildAgent("claude"), BuildAgent("gpt") };
        _agentService.GetAgentsAsync(Arg.Any<CancellationToken>()).Returns(agents);
        await _sut.LoadAgentsCommand.ExecuteAsync(null);

        // Act
        await _sut.SelectAgentCommand.ExecuteAsync("claude");

        // Assert
        _sut.SelectedAgentName.Should().Be("claude");
    }

    [Fact]
    public async Task SelectAgentCommand_UpdatesIsSelectedInCollection()
    {
        // Arrange
        var agents = new List<AgentDto> { BuildAgent("claude"), BuildAgent("gpt") };
        _agentService.GetAgentsAsync(Arg.Any<CancellationToken>()).Returns(agents);
        await _sut.LoadAgentsCommand.ExecuteAsync(null);

        // Act
        await _sut.SelectAgentCommand.ExecuteAsync("gpt");

        // Assert
        _sut.Agents.Should().ContainSingle(a => a.Name == "gpt" && a.IsSelected);
        _sut.Agents.Should().ContainSingle(a => a.Name == "claude" && !a.IsSelected);
    }

    [Fact]
    public async Task SelectAgentCommand_ClosesPopup()
    {
        // Arrange
        var agents = new List<AgentDto> { BuildAgent("claude") };
        _agentService.GetAgentsAsync(Arg.Any<CancellationToken>()).Returns(agents);
        await _sut.LoadAgentsCommand.ExecuteAsync(null);

        // Act
        await _sut.SelectAgentCommand.ExecuteAsync("claude");

        // Assert
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
    }
}
