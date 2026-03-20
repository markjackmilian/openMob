using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Services;

namespace openMob.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AgentService"/>.
/// </summary>
public sealed class AgentServiceTests
{
    private readonly IOpencodeApiClient _apiClient;
    private readonly AgentService _sut;

    public AgentServiceTests()
    {
        _apiClient = Substitute.For<IOpencodeApiClient>();
        _sut = new AgentService(_apiClient);
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private static AgentDto BuildAgent(
        string name = "claude",
        string? description = "Claude AI",
        bool builtIn = true)
    {
        return new AgentDto(
            Name: name, Description: description, Mode: "primary", BuiltIn: builtIn,
            TopP: null, Temperature: null, Color: null, Model: null, Prompt: null,
            Tools: default, Options: default, MaxSteps: null, Permission: default,
            Hidden: false);
    }

    // ─── GetAgentsAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetAgentsAsync_WhenApiReturnsSuccess_ReturnsAgentList()
    {
        // Arrange
        var agents = new List<AgentDto> { BuildAgent("claude"), BuildAgent("gpt") };
        _apiClient.GetAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<AgentDto>>.Success(agents));

        // Act
        var result = await _sut.GetAgentsAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAgentsAsync_WhenApiReturnsFailure_ReturnsEmptyList()
    {
        // Arrange
        _apiClient.GetAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<AgentDto>>.Failure(
                new OpencodeApiError(ErrorKind.ServerError, "Server error", 500, null)));

        // Act
        var result = await _sut.GetAgentsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAgentsAsync_CallsApiClientExactlyOnce()
    {
        // Arrange
        _apiClient.GetAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<AgentDto>>.Success(new List<AgentDto>()));

        // Act
        await _sut.GetAgentsAsync();

        // Assert
        await _apiClient.Received(1).GetAgentsAsync(Arg.Any<CancellationToken>());
    }

    // ─── GetPrimaryAgentsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetPrimaryAgentsAsync_WhenApiReturnsMixedModes_ReturnsOnlyPrimaryAndAll()
    {
        // Arrange
        var agents = new List<AgentDto>
        {
            BuildAgentWithMode("primary-agent", "primary"),
            BuildAgentWithMode("subagent-agent", "subagent"),
            BuildAgentWithMode("all-agent", "all"),
            BuildAgentWithMode("another-primary", "primary"),
        };
        _apiClient.GetAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<AgentDto>>.Success(agents));

        // Act
        var result = await _sut.GetPrimaryAgentsAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(a => a.Name == "primary-agent");
        result.Should().Contain(a => a.Name == "all-agent");
        result.Should().Contain(a => a.Name == "another-primary");
    }

    [Fact]
    public async Task GetPrimaryAgentsAsync_WhenApiReturnsOnlySubagents_ReturnsEmptyList()
    {
        // Arrange
        var agents = new List<AgentDto>
        {
            BuildAgentWithMode("sub1", "subagent"),
            BuildAgentWithMode("sub2", "subagent"),
        };
        _apiClient.GetAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<AgentDto>>.Success(agents));

        // Act
        var result = await _sut.GetPrimaryAgentsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPrimaryAgentsAsync_WhenApiReturnsEmptyList_ReturnsEmptyList()
    {
        // Arrange
        _apiClient.GetAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<AgentDto>>.Success(new List<AgentDto>()));

        // Act
        var result = await _sut.GetPrimaryAgentsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPrimaryAgentsAsync_WhenApiReturnsAllModeAgent_IncludesItInResult()
    {
        // Arrange
        var agents = new List<AgentDto>
        {
            BuildAgentWithMode("universal-agent", "all"),
        };
        _apiClient.GetAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<AgentDto>>.Success(agents));

        // Act
        var result = await _sut.GetPrimaryAgentsAsync();

        // Assert
        result.Should().ContainSingle(a => a.Name == "universal-agent");
    }

    private static AgentDto BuildAgentWithMode(string name, string mode, bool hidden = false)
    {
        return new AgentDto(
            Name: name, Description: null, Mode: mode, BuiltIn: true,
            TopP: null, Temperature: null, Color: null, Model: null, Prompt: null,
            Tools: default, Options: default, MaxSteps: null, Permission: default,
            Hidden: hidden);
    }

    // ─── GetPrimaryAgentsAsync — Hidden filter ────────────────────────────────

    [Fact]
    public async Task GetPrimaryAgentsAsync_WhenAgentIsHidden_ExcludesItFromResult()
    {
        // Arrange
        var agents = new List<AgentDto>
        {
            BuildAgentWithMode("visible-primary", "primary", hidden: false),
            BuildAgentWithMode("hidden-primary", "primary", hidden: true),
        };
        _apiClient.GetAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<AgentDto>>.Success(agents));

        // Act
        var result = await _sut.GetPrimaryAgentsAsync();

        // Assert
        result.Should().ContainSingle(a => a.Name == "visible-primary");
        result.Should().NotContain(a => a.Name == "hidden-primary");
    }

    [Fact]
    public async Task GetPrimaryAgentsAsync_WhenAgentIsNotHidden_IncludesItInResult()
    {
        // Arrange
        var agents = new List<AgentDto>
        {
            BuildAgentWithMode("visible-primary", "primary", hidden: false),
        };
        _apiClient.GetAgentsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<AgentDto>>.Success(agents));

        // Act
        var result = await _sut.GetPrimaryAgentsAsync();

        // Assert
        result.Should().ContainSingle(a => a.Name == "visible-primary");
    }
}
