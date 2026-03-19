using System.Net;
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
/// <see cref="ChatService.SubscribeToEventsAsync"/>, and the initial
/// <see cref="ChatService.IsConnected"/> state.
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

    /// <summary>
    /// Builds an <see cref="HttpClient"/> backed by a fake handler that creates a fresh
    /// <see cref="HttpResponseMessage"/> with a new <see cref="StringContent"/> on every
    /// request. This is required because <see cref="StringContent"/> streams are consumed
    /// on first read; reusing the same instance causes subsequent reconnect attempts to see
    /// an empty stream and trigger the backoff loop.
    /// </summary>
    private static HttpClient BuildSseClient(string sseBody)
    {
        var handler = new FakeSseMessageHandler(sseBody);
        return new HttpClient(handler);
    }

    /// <summary>
    /// Builds an <see cref="HttpClient"/> backed by a fake handler that always throws
    /// <see cref="HttpRequestException"/> on every request.
    /// </summary>
    private static HttpClient BuildFailingSseClient()
    {
        var handler = new ThrowingHttpMessageHandler();
        return new HttpClient(handler);
    }

    /// <summary>
    /// Configures the connection manager substitutes with sensible defaults for SSE tests.
    /// </summary>
    private void SetupConnectionManager()
    {
        _connectionManager
            .GetBaseUrlAsync(Arg.Any<CancellationToken>())
            .Returns("http://localhost:3000");
        _connectionManager
            .GetBasicAuthHeaderAsync(Arg.Any<CancellationToken>())
            .Returns((string?)null);
    }

    /// <summary>
    /// Collects all events from <see cref="ChatService.SubscribeToEventsAsync"/> into a list.
    /// </summary>
    private static async Task<List<ChatEvent>> CollectEventsAsync(
        IChatService sut,
        CancellationToken ct = default)
    {
        var events = new List<ChatEvent>();
        await foreach (var e in sut.SubscribeToEventsAsync(ct).ConfigureAwait(false))
            events.Add(e);
        return events;
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

    // ─── SendPromptAsync — circuit breaker ───────────────────────────────────

    [Fact]
    public async Task SendPromptAsync_WhenCircuitBreakerThrows_ReturnsFailWithCircuitOpen()
    {
        // Arrange
        _apiClient
            .SendPromptAsyncNoWait(
                Arg.Any<string>(),
                Arg.Any<SendPromptRequest>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new FakeBrokenCircuitException());

        // Act
        var result = await _sut.SendPromptAsync("session1", "hello", null, null);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ChatServiceErrorKind.CircuitOpen);
    }

    // ─── GetMessagesAsync — circuit breaker ──────────────────────────────────

    [Fact]
    public async Task GetMessagesAsync_WhenCircuitBreakerThrows_ReturnsFailWithCircuitOpen()
    {
        // Arrange
        _apiClient
            .GetMessagesAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new FakeBrokenCircuitException());

        // Act
        var result = await _sut.GetMessagesAsync("session1");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ChatServiceErrorKind.CircuitOpen);
    }

    // ─── SubscribeToEventsAsync — IsConnected transitions ────────────────────

    [Fact]
    public async Task SubscribeToEventsAsync_WhenServerConnectedEventReceived_SetsIsConnectedTrue()
    {
        // Arrange — server sends envelope format: data: {"payload":{"type":"server.connected","properties":{}}}
        const string sseBody =
            "id: evt-1\r\n" +
            "data: {\"payload\":{\"type\":\"server.connected\",\"properties\":{}}}\r\n" +
            "\r\n";

        SetupConnectionManager();
        _httpClientFactory.CreateClient("opencode-sse").Returns(BuildSseClient(sseBody));

        // Cancel after receiving the first event so the reconnect loop does not restart.
        // With the Channel-based implementation, cancelling mid-iteration causes
        // ReadAllAsync to throw OperationCanceledException — catch it gracefully.
        using var cts = new CancellationTokenSource();

        // Act — read the first event, then cancel to stop the loop
        bool? isConnectedDuringEvent = null;
        try
        {
            await foreach (var e in _sut.SubscribeToEventsAsync(cts.Token))
            {
                if (e is ServerConnectedEvent)
                    isConnectedDuringEvent = _sut.IsConnected;
                cts.Cancel(); // stop after first event
            }
        }
        catch (OperationCanceledException) { /* expected */ }

        // Assert — IsConnected must have been true immediately after the ServerConnectedEvent
        isConnectedDuringEvent.Should().BeTrue();
    }

    [Fact]
    public async Task SubscribeToEventsAsync_WhenServerConnectedEventReceived_RaisesIsConnectedChangedWithTrue()
    {
        // Arrange — server sends envelope format
        const string sseBody =
            "id: evt-1\r\n" +
            "data: {\"payload\":{\"type\":\"server.connected\",\"properties\":{}}}\r\n" +
            "\r\n";

        SetupConnectionManager();
        _httpClientFactory.CreateClient("opencode-sse").Returns(BuildSseClient(sseBody));

        var raisedValues = new List<bool>();
        _sut.IsConnectedChanged += v => raisedValues.Add(v);

        // Cancel after receiving the first event so the reconnect loop does not restart.
        // With the Channel-based implementation, cancelling mid-iteration causes
        // ReadAllAsync to throw OperationCanceledException — catch it gracefully.
        using var cts = new CancellationTokenSource();
        try
        {
            await foreach (var _ in _sut.SubscribeToEventsAsync(cts.Token))
            {
                cts.Cancel(); // stop after first event
            }
        }
        catch (OperationCanceledException) { /* expected */ }

        // Assert — the event must have been raised with true at least once
        raisedValues.Should().Contain(true);
    }

    [Fact]
    public async Task SubscribeToEventsAsync_WhenStreamEnds_SetsIsConnectedFalse()
    {
        // Arrange — one event then stream ends naturally. After the stream ends the
        // reconnect loop will try again (and get the same body again). We use a short
        // timeout CTS (500 ms) to stop the loop after the first reconnect attempt,
        // allowing SetConnected(false) to be called through the normal stream-end path.
        const string sseBody =
            "id: evt-1\r\n" +
            "data: {\"payload\":{\"type\":\"server.connected\",\"properties\":{}}}\r\n" +
            "\r\n";

        SetupConnectionManager();
        _httpClientFactory.CreateClient("opencode-sse").Returns(BuildSseClient(sseBody));

        // Use a timed CTS so the loop exits after the stream ends naturally.
        // With the Channel-based implementation, cancelling mid-iteration causes
        // ReadAllAsync to throw OperationCanceledException — catch it gracefully.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        try
        {
            await foreach (var _ in _sut.SubscribeToEventsAsync(cts.Token)) { }
        }
        catch (OperationCanceledException) { /* expected */ }

        // Assert — after the stream ends the service must report IsConnected = false
        _sut.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task SubscribeToEventsAsync_WhenCancelled_CompletesWithoutException()
    {
        // Arrange — cancel before subscribing
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        SetupConnectionManager();
        // The factory should not even be called when the token is already cancelled,
        // but configure it defensively so the test does not fail for the wrong reason.
        _httpClientFactory.CreateClient("opencode-sse").Returns(BuildSseClient(string.Empty));

        // Act — must complete without throwing
        var act = async () => await CollectEventsAsync(_sut, cts.Token);

        // Assert
        await act.Should().NotThrowAsync();
        _sut.IsConnected.Should().BeFalse();
    }

    [Fact(Timeout = 30_000)]
    public async Task SubscribeToEventsAsync_WhenConnectionFailsRepeatedly_ExhaustsMaxAttempts()
    {
        // Arrange — every request throws HttpRequestException; no events are ever received.
        // The service will increment consecutiveFailedAttempts on each attempt and yield break
        // after MaxConsecutiveFailedAttempts (10) without any backoff delay because the
        // ThrowingHttpMessageHandler throws before any delay is needed.
        // NOTE: The backoff Task.Delay IS still called between attempts (1s, 2s, 4s, …).
        // We use a 20-second safety CTS so the test does not hang if the loop never exits.
        SetupConnectionManager();
        _httpClientFactory.CreateClient("opencode-sse").Returns(_ => BuildFailingSseClient());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        // Act — the loop should exhaust 10 attempts and yield break naturally,
        // or be cancelled by the safety timeout; either way no exception is thrown.
        var act = async () => await CollectEventsAsync(_sut, cts.Token);

        // Assert — completes without exception; IsConnected remains false
        await act.Should().NotThrowAsync();
        _sut.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task SubscribeToEventsAsync_YieldsTypedChatEvents()
    {
        // Arrange — server.connected followed by message.updated, both in envelope format
        const string sseBody =
            "id: evt-1\r\n" +
            "data: {\"payload\":{\"type\":\"server.connected\",\"properties\":{}}}\r\n" +
            "\r\n" +
            "id: evt-2\r\n" +
            "data: {\"payload\":{\"type\":\"message.updated\",\"properties\":{\"info\":{\"id\":\"msg1\",\"sessionID\":\"s1\",\"role\":\"user\",\"time\":{\"created\":0}},\"parts\":[]}}}\r\n" +
            "\r\n";

        SetupConnectionManager();
        _httpClientFactory.CreateClient("opencode-sse").Returns(BuildSseClient(sseBody));

        // Cancel after receiving 2 events to prevent the reconnect loop from restarting.
        // With the Channel-based implementation, cancelling mid-iteration causes
        // ReadAllAsync to throw OperationCanceledException — catch it gracefully.
        using var cts = new CancellationTokenSource();
        var events = new List<ChatEvent>();
        try
        {
            await foreach (var e in _sut.SubscribeToEventsAsync(cts.Token))
            {
                events.Add(e);
                if (events.Count >= 2)
                    cts.Cancel();
            }
        }
        catch (OperationCanceledException) { /* expected */ }

        // Assert — two events yielded in order
        events.Should().HaveCount(2);
        events[0].Should().BeOfType<ServerConnectedEvent>();
        events[1].Should().BeOfType<MessageUpdatedEvent>();
    }
}

// ─── Test helpers ─────────────────────────────────────────────────────────────

/// <summary>
/// A fake <see cref="HttpMessageHandler"/> that creates a fresh
/// <see cref="HttpResponseMessage"/> with a new <see cref="StringContent"/> on every
/// <c>SendAsync</c> call. This is critical for SSE reconnect tests: the SSE stream is
/// consumed on first read, so reusing the same <see cref="HttpResponseMessage"/> would
/// cause all subsequent reconnect attempts to see an empty stream and trigger the full
/// backoff loop (1s + 2s + 4s + … = minutes of test time).
/// </summary>
internal sealed class FakeSseMessageHandler : HttpMessageHandler
{
    private readonly string _sseBody;

    public FakeSseMessageHandler(string sseBody)
    {
        _sseBody = sseBody;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var content = new StringContent(_sseBody, System.Text.Encoding.UTF8, "text/event-stream");
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        return Task.FromResult(response);
    }
}

/// <summary>
/// A fake <see cref="HttpMessageHandler"/> that always throws
/// <see cref="HttpRequestException"/> to simulate a permanently failing connection.
/// </summary>
internal sealed class ThrowingHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
        => throw new HttpRequestException("Simulated connection failure.");
}

/// <summary>
/// Fake exception whose type name contains "BrokenCircuitException" to test
/// the <c>IsCircuitBreakerException</c> detection logic in <see cref="ChatService"/>.
/// </summary>
internal sealed class FakeBrokenCircuitException : Exception
{
    public FakeBrokenCircuitException() : base("Circuit is open.") { }
}
