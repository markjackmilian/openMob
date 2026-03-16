using System.Net.Http;
using NSubstitute.ExceptionExtensions;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;
using openMob.Core.Models;
using openMob.Core.Services;

namespace openMob.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ChatService"/>.
/// Covers <see cref="ChatService.SendPromptAsync"/>, <see cref="ChatService.GetMessagesAsync"/>,
/// and the initial <see cref="ChatService.IsConnected"/> state.
/// SSE reconnect integration tests are deferred to a future spec.
/// </summary>
public sealed class ChatServiceTests
{
    private readonly IOpencodeApiClient _apiClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOpencodeConnectionManager _connectionManager;
    private readonly ChatService _sut;

    public ChatServiceTests()
    {
        _apiClient = Substitute.For<IOpencodeApiClient>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _connectionManager = Substitute.For<IOpencodeConnectionManager>();
        _sut = new ChatService(_apiClient, _httpClientFactory, _connectionManager);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static MessageWithPartsDto BuildMessage(string id = "msg-1", string sessionId = "sess-1")
    {
        var time = System.Text.Json.JsonSerializer.SerializeToElement(new { created = 0L });
        return new MessageWithPartsDto(
            Info: new MessageInfoDto(id, sessionId, "user", time),
            Parts: Array.Empty<PartDto>());
    }

    // ─── IsConnected initial state ────────────────────────────────────────────

    [Fact]
    public void IsConnected_InitiallyFalse()
    {
        // Assert
        _sut.IsConnected.Should().BeFalse();
    }

    // ─── SendPromptAsync — happy path ─────────────────────────────────────────

    [Fact]
    public async Task SendPromptAsync_WhenApiReturnsSuccess_ReturnsOkResult()
    {
        // Arrange
        _apiClient
            .SendPromptAsyncNoWait(
                Arg.Any<string>(),
                Arg.Any<SendPromptRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<bool>.Success(true));

        // Act
        var result = await _sut.SendPromptAsync("sess-1", "Hello", null, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    // ─── SendPromptAsync — error mapping ──────────────────────────────────────

    [Fact]
    public async Task SendPromptAsync_WhenApiReturnsNetworkError_ReturnsFailWithNetworkError()
    {
        // Arrange
        _apiClient
            .SendPromptAsyncNoWait(
                Arg.Any<string>(),
                Arg.Any<SendPromptRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<bool>.Failure(
                new OpencodeApiError(ErrorKind.NetworkUnreachable, "net error", null, null)));

        // Act
        var result = await _sut.SendPromptAsync("sess-1", "Hello", null, null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ChatServiceErrorKind.NetworkError);
    }

    [Fact]
    public async Task SendPromptAsync_WhenApiReturnsServerError_ReturnsFailWithServerError()
    {
        // Arrange
        _apiClient
            .SendPromptAsyncNoWait(
                Arg.Any<string>(),
                Arg.Any<SendPromptRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<bool>.Failure(
                new OpencodeApiError(ErrorKind.ServerError, "server error", 500, null)));

        // Act
        var result = await _sut.SendPromptAsync("sess-1", "Hello", null, null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ChatServiceErrorKind.ServerError);
        result.Error.HttpStatusCode.Should().Be(500);
    }

    [Fact]
    public async Task SendPromptAsync_WhenApiReturnsTimeout_ReturnsFailWithTimeout()
    {
        // Arrange
        _apiClient
            .SendPromptAsyncNoWait(
                Arg.Any<string>(),
                Arg.Any<SendPromptRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<bool>.Failure(
                new OpencodeApiError(ErrorKind.Timeout, "timeout", null, null)));

        // Act
        var result = await _sut.SendPromptAsync("sess-1", "Hello", null, null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ChatServiceErrorKind.Timeout);
    }

    // ─── SendPromptAsync — cancellation ──────────────────────────────────────

    [Fact]
    public async Task SendPromptAsync_WhenCancelled_ReturnsFailWithCancelled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var ct = cts.Token;

        _apiClient
            .SendPromptAsyncNoWait(
                Arg.Any<string>(),
                Arg.Any<SendPromptRequest>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException(ct));

        // Act
        var result = await _sut.SendPromptAsync("sess-1", "Hello", null, null, ct);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ChatServiceErrorKind.Cancelled);
    }

    // ─── SendPromptAsync — delegates correct arguments ────────────────────────

    [Fact]
    public async Task SendPromptAsync_DelegatesSessionIdToApiClient()
    {
        // Arrange
        _apiClient
            .SendPromptAsyncNoWait(
                Arg.Any<string>(),
                Arg.Any<SendPromptRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<bool>.Success(true));

        // Act
        await _sut.SendPromptAsync("my-session", "Hello", null, null);

        // Assert
        await _apiClient.Received(1).SendPromptAsyncNoWait(
            "my-session",
            Arg.Any<SendPromptRequest>(),
            Arg.Any<CancellationToken>());
    }

    // ─── GetMessagesAsync — happy path ────────────────────────────────────────

    [Fact]
    public async Task GetMessagesAsync_WhenApiReturnsMessages_ReturnsOkWithList()
    {
        // Arrange
        var messages = new List<MessageWithPartsDto>
        {
            BuildMessage("msg-1"),
            BuildMessage("msg-2"),
        };
        _apiClient
            .GetMessagesAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<MessageWithPartsDto>>.Success(messages));

        // Act
        var result = await _sut.GetMessagesAsync("sess-1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Count.Should().Be(2);
    }

    // ─── GetMessagesAsync — error path ────────────────────────────────────────

    [Fact]
    public async Task GetMessagesAsync_WhenApiReturnsError_ReturnsFailResult()
    {
        // Arrange
        _apiClient
            .GetMessagesAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<MessageWithPartsDto>>.Failure(
                new OpencodeApiError(ErrorKind.ServerError, "server error", 500, null)));

        // Act
        var result = await _sut.GetMessagesAsync("sess-1");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GetMessagesAsync_WhenApiReturnsNetworkError_MapsToNetworkErrorKind()
    {
        // Arrange
        _apiClient
            .GetMessagesAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<MessageWithPartsDto>>.Failure(
                new OpencodeApiError(ErrorKind.NetworkUnreachable, "net error", null, null)));

        // Act
        var result = await _sut.GetMessagesAsync("sess-1");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ChatServiceErrorKind.NetworkError);
    }

    // ─── GetMessagesAsync — cancellation ─────────────────────────────────────

    [Fact]
    public async Task GetMessagesAsync_WhenCancelled_ReturnsFailWithCancelled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var ct = cts.Token;

        _apiClient
            .GetMessagesAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException(ct));

        // Act
        var result = await _sut.GetMessagesAsync("sess-1", ct: ct);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ChatServiceErrorKind.Cancelled);
    }

    // ─── GetMessagesAsync — delegates correct arguments ───────────────────────

    [Fact]
    public async Task GetMessagesAsync_DelegatesSessionIdAndLimitToApiClient()
    {
        // Arrange
        _apiClient
            .GetMessagesAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<IReadOnlyList<MessageWithPartsDto>>.Success(
                new List<MessageWithPartsDto>()));

        // Act
        await _sut.GetMessagesAsync("sess-42", limit: 10);

        // Assert
        await _apiClient.Received(1).GetMessagesAsync(
            "sess-42",
            10,
            Arg.Any<CancellationToken>());
    }
}
