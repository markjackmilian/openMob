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
            Tools: default, Options: default, MaxSteps: null, Permission: default);
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
}
