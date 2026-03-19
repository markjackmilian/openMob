using NSubstitute.ExceptionExtensions;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;
using openMob.Core.Models;
using openMob.Core.Services;

namespace openMob.Tests.Services;

/// <summary>
/// Unit tests for <see cref="CommandService"/>.
/// Covers command loading, caching, searching, execution, and cache invalidation.
/// </summary>
public sealed class CommandServiceTests
{
    private readonly IOpencodeApiClient _apiClient;
    private readonly CommandService _sut;

    public CommandServiceTests()
    {
        _apiClient = Substitute.For<IOpencodeApiClient>();
        _sut = new CommandService(_apiClient);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static IReadOnlyList<CommandDto> BuildCommandDtos()
    {
        return new List<CommandDto>
        {
            new("test", "Run tests", null, null, "Run all tests", false),
            new("lint", "Lint code", null, null, "Lint the codebase", true),
            new("deploy", "Deploy app", null, null, "Deploy to production", false),
        };
    }

    // ─── GetCommandsAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetCommandsAsync_WhenFirstCall_CallsApiAndReturnsCommands()
    {
        // Arrange
        var dtos = BuildCommandDtos();
        _apiClient.GetCommandsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<CommandDto>>.Success(dtos));

        // Act
        var result = await _sut.GetCommandsAsync();

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("test");
        result[0].Description.Should().Be("Run tests");
        result[1].IsSubtask.Should().BeTrue();
    }

    [Fact]
    public async Task GetCommandsAsync_WhenCalledTwice_ReturnsCachedResultWithoutSecondApiCall()
    {
        // Arrange
        var dtos = BuildCommandDtos();
        _apiClient.GetCommandsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<CommandDto>>.Success(dtos));

        // Act
        await _sut.GetCommandsAsync();
        var result = await _sut.GetCommandsAsync();

        // Assert
        result.Should().HaveCount(3);
        await _apiClient.Received(1).GetCommandsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCommandsAsync_WhenApiFails_ReturnsEmptyList()
    {
        // Arrange
        var error = new OpencodeApiError(ErrorKind.ServerError, "Server error", 500, null);
        _apiClient.GetCommandsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<CommandDto>>.Failure(error));

        // Act
        var result = await _sut.GetCommandsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCommandsAsync_WhenApiThrows_ReturnsEmptyList()
    {
        // Arrange
        _apiClient.GetCommandsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _sut.GetCommandsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    // ─── SearchCommandsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task SearchCommandsAsync_WhenQueryMatchesName_ReturnsMatchingCommands()
    {
        // Arrange
        var dtos = BuildCommandDtos();
        _apiClient.GetCommandsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<CommandDto>>.Success(dtos));

        // Act
        var result = await _sut.SearchCommandsAsync("test");

        // Assert
        result.Should().ContainSingle();
        result[0].Name.Should().Be("test");
    }

    [Fact]
    public async Task SearchCommandsAsync_WhenQueryMatchesDescription_ReturnsMatchingCommands()
    {
        // Arrange
        var dtos = BuildCommandDtos();
        _apiClient.GetCommandsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<CommandDto>>.Success(dtos));

        // Act
        var result = await _sut.SearchCommandsAsync("Lint code");

        // Assert
        result.Should().ContainSingle();
        result[0].Name.Should().Be("lint");
    }

    [Fact]
    public async Task SearchCommandsAsync_WhenQueryMatchesNothing_ReturnsEmptyList()
    {
        // Arrange
        var dtos = BuildCommandDtos();
        _apiClient.GetCommandsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<CommandDto>>.Success(dtos));

        // Act
        var result = await _sut.SearchCommandsAsync("nonexistent");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchCommandsAsync_WhenEmptyQuery_ReturnsAllCommands()
    {
        // Arrange
        var dtos = BuildCommandDtos();
        _apiClient.GetCommandsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<CommandDto>>.Success(dtos));

        // Act
        var result = await _sut.SearchCommandsAsync("");

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task SearchCommandsAsync_WhenWhitespaceQuery_ReturnsAllCommands()
    {
        // Arrange
        var dtos = BuildCommandDtos();
        _apiClient.GetCommandsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<CommandDto>>.Success(dtos));

        // Act
        var result = await _sut.SearchCommandsAsync("   ");

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task SearchCommandsAsync_IsCaseInsensitive()
    {
        // Arrange
        var dtos = BuildCommandDtos();
        _apiClient.GetCommandsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<CommandDto>>.Success(dtos));

        // Act
        var result = await _sut.SearchCommandsAsync("DEPLOY");

        // Assert
        result.Should().ContainSingle();
        result[0].Name.Should().Be("deploy");
    }

    // ─── ExecuteCommandAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteCommandAsync_WhenCommandSucceeds_ReturnsSuccessResult()
    {
        // Arrange
        var messageDto = new MessageWithPartsDto(
            Info: new MessageInfoDto("msg-1", "sess-1", "assistant",
                System.Text.Json.JsonSerializer.SerializeToElement(new { created = 1710576000000L })),
            Parts: Array.Empty<PartDto>());
        _apiClient.SendCommandAsync("sess-1", Arg.Any<SendCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<MessageWithPartsDto>.Success(messageDto));

        // Act
        var result = await _sut.ExecuteCommandAsync("sess-1", "test");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteCommandAsync_CallsSendCommandAsyncWithCorrectArguments()
    {
        // Arrange
        var messageDto = new MessageWithPartsDto(
            Info: new MessageInfoDto("msg-1", "sess-1", "assistant",
                System.Text.Json.JsonSerializer.SerializeToElement(new { created = 1710576000000L })),
            Parts: Array.Empty<PartDto>());
        _apiClient.SendCommandAsync(Arg.Any<string>(), Arg.Any<SendCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<MessageWithPartsDto>.Success(messageDto));

        // Act
        await _sut.ExecuteCommandAsync("sess-1", "test");

        // Assert
        await _apiClient.Received(1).SendCommandAsync(
            "sess-1",
            Arg.Is<SendCommandRequest>(r => r.Name == "test"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteCommandAsync_WhenApiFails_ReturnsFailResult()
    {
        // Arrange
        var error = new OpencodeApiError(ErrorKind.ServerError, "Command failed", 500, null);
        _apiClient.SendCommandAsync("sess-1", Arg.Any<SendCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<MessageWithPartsDto>.Failure(error));

        // Act
        var result = await _sut.ExecuteCommandAsync("sess-1", "test");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ChatServiceErrorKind.ServerError);
    }

    [Fact]
    public async Task ExecuteCommandAsync_WhenApiThrows_ReturnsFailResult()
    {
        // Arrange
        _apiClient.SendCommandAsync("sess-1", Arg.Any<SendCommandRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _sut.ExecuteCommandAsync("sess-1", "test");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(ChatServiceErrorKind.Unknown);
    }

    [Fact]
    public async Task ExecuteCommandAsync_WhenCancelled_ReturnsFailResultWithCancelledKind()
    {
        // Arrange
        _apiClient.SendCommandAsync("sess-1", Arg.Any<SendCommandRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _sut.ExecuteCommandAsync("sess-1", "test");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ChatServiceErrorKind.Cancelled);
    }

    [Fact]
    public void ExecuteCommandAsync_WhenSessionIdIsNull_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _sut.ExecuteCommandAsync(null!, "test");

        // Assert
        act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void ExecuteCommandAsync_WhenCommandNameIsNull_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _sut.ExecuteCommandAsync("sess-1", null!);

        // Assert
        act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── InvalidateCache ─────────────────────────────────────────────────────

    [Fact]
    public async Task InvalidateCache_WhenCalled_NextGetCommandsCallsApiAgain()
    {
        // Arrange
        var dtos = BuildCommandDtos();
        _apiClient.GetCommandsAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<CommandDto>>.Success(dtos));

        // First call — populates cache
        await _sut.GetCommandsAsync();

        // Act
        _sut.InvalidateCache();
        await _sut.GetCommandsAsync();

        // Assert — API should have been called twice (once before invalidation, once after)
        await _apiClient.Received(2).GetCommandsAsync(Arg.Any<CancellationToken>());
    }
}
