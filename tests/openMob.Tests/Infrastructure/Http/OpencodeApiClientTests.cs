using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;
using openMob.Core.Services;
using openMob.Tests.Helpers;

namespace openMob.Tests.Infrastructure.Http;

/// <summary>
/// Unit tests for <see cref="OpencodeApiClient"/>.
/// Uses <see cref="MockHttpMessageHandler"/> to intercept HTTP calls without real network I/O.
/// </summary>
public sealed class OpencodeApiClientTests
{
    private readonly MockHttpMessageHandler _handler;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOpencodeConnectionManager _connectionManager;
    private readonly FakeOpencodeSettingsService _settingsService;
    private readonly IActiveProjectService _activeProjectService;

    public OpencodeApiClientTests()
    {
        _handler = new MockHttpMessageHandler();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _httpClientFactory.CreateClient("opencode").Returns(new HttpClient(_handler));

        _connectionManager = Substitute.For<IOpencodeConnectionManager>();
        _connectionManager.GetBaseUrlAsync(Arg.Any<CancellationToken>())
            .Returns("http://localhost:4096");
        _connectionManager.GetBasicAuthHeaderAsync(Arg.Any<CancellationToken>())
            .Returns((string?)null);

        _settingsService = new FakeOpencodeSettingsService { TimeoutSeconds = 30 };

        _activeProjectService = Substitute.For<IActiveProjectService>();
        _activeProjectService.GetActiveProjectAsync(Arg.Any<CancellationToken>())
            .Returns(new ProjectDto(
                Id: "proj-1",
                Worktree: "/home/user/myproject",
                VcsDir: null,
                Vcs: "git",
                Time: new ProjectTimeDto(Created: 1710000000000, Initialized: null)));
    }

    /// <summary>
    /// Creates the SUT with production retry delays (for non-retry tests).
    /// </summary>
    private OpencodeApiClient CreateSut()
        => new(_httpClientFactory, _connectionManager, _settingsService, _activeProjectService);

    /// <summary>
    /// Creates the SUT with zero retry delays so retry tests complete instantly.
    /// </summary>
    private OpencodeApiClient CreateSutWithZeroDelays()
        => new(_httpClientFactory, _connectionManager, _settingsService, _activeProjectService,
            retryDelays: [TimeSpan.Zero, TimeSpan.Zero]);

    // ──────────────────────────────────────────────────────────────
    // Happy path — GetHealthAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetHealthAsync_WhenServerReturns200_ReturnsSuccessWithHealthDto()
    {
        // Arrange
        _handler.SetupResponse("GET", "/global/health", HttpStatusCode.OK,
            """{"healthy":true,"version":"1.2.3"}""");
        var sut = CreateSut();

        // Act
        var result = await sut.GetHealthAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetHealthAsync_WhenServerReturns200_HealthDtoHasCorrectValues()
    {
        // Arrange
        _handler.SetupResponse("GET", "/global/health", HttpStatusCode.OK,
            """{"healthy":true,"version":"1.2.3"}""");
        var sut = CreateSut();

        // Act
        var result = await sut.GetHealthAsync();

        // Assert
        result.Value!.Healthy.Should().BeTrue();
        result.Value.Version.Should().Be("1.2.3");
    }

    // ──────────────────────────────────────────────────────────────
    // Happy path — GetSessionsAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSessionsAsync_WhenServerReturns200_ReturnsSuccessWithSessionList()
    {
        // Arrange
        _handler.SetupResponse("GET", "/session", HttpStatusCode.OK,
            """
            [
              {
                "id":"sess-1","projectID":"proj-1","directory":"/home","parentID":null,
                "summary":null,"share":null,"title":"My Session","version":"1",
                "time":{"created":1000,"updated":2000,"compacting":null},"revert":null
              }
            ]
            """);
        var sut = CreateSut();

        // Act
        var result = await sut.GetSessionsAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Id.Should().Be("sess-1");
    }

    // ──────────────────────────────────────────────────────────────
    // Happy path — CreateSessionAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSessionAsync_WhenServerReturns200_ReturnsSuccessWithSessionDto()
    {
        // Arrange
        _handler.SetupResponse("POST", "/session", HttpStatusCode.OK,
            """
            {
              "id":"new-sess","projectID":"proj-1","directory":"/home","parentID":null,
              "summary":null,"share":null,"title":"New Session","version":"1",
              "time":{"created":1000,"updated":1000,"compacting":null},"revert":null
            }
            """);
        var sut = CreateSut();
        var request = new CreateSessionRequest(Title: "New Session", ParentId: string.Empty);

        // Act
        var result = await sut.CreateSessionAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be("new-sess");
        result.Value.Title.Should().Be("New Session");
    }

    // ──────────────────────────────────────────────────────────────
    // Happy path — DeleteSessionAsync (204 No Content)
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteSessionAsync_WhenServerReturns204_ReturnsSuccessTrue()
    {
        // Arrange
        _handler.SetupResponse("DELETE", "/session/sess-1", HttpStatusCode.NoContent, "");
        var sut = CreateSut();

        // Act
        var result = await sut.DeleteSessionAsync("sess-1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────
    // No active server
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetHealthAsync_WhenNoActiveServer_ReturnsNoActiveServerError()
    {
        // Arrange
        _connectionManager.GetBaseUrlAsync(Arg.Any<CancellationToken>())
            .Returns((string?)null);
        var sut = CreateSut();

        // Act
        var result = await sut.GetHealthAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.NoActiveServer);
    }

    [Fact]
    public async Task GetHealthAsync_WhenNoActiveServer_DoesNotMakeHttpRequest()
    {
        // Arrange
        _connectionManager.GetBaseUrlAsync(Arg.Any<CancellationToken>())
            .Returns((string?)null);
        var sut = CreateSut();

        // Act
        await sut.GetHealthAsync();

        // Assert
        _handler.CallCount.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────
    // HTTP error mapping
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetHealthAsync_WhenServerReturns401_ReturnsUnauthorizedError()
    {
        // Arrange
        _handler.SetupResponse("GET", "/global/health", HttpStatusCode.Unauthorized, "");
        var sut = CreateSut();

        // Act
        var result = await sut.GetHealthAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.Unauthorized);
        result.Error.HttpStatusCode.Should().Be(401);
    }

    [Fact]
    public async Task GetHealthAsync_WhenServerReturns404_ReturnsNotFoundError()
    {
        // Arrange
        _handler.SetupResponse("GET", "/global/health", HttpStatusCode.NotFound, "");
        var sut = CreateSut();

        // Act
        var result = await sut.GetHealthAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.NotFound);
        result.Error.HttpStatusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetHealthAsync_WhenServerReturns500_ReturnsServerError()
    {
        // Arrange
        _handler.SetupResponse("GET", "/global/health", HttpStatusCode.InternalServerError, "Internal Server Error");
        var sut = CreateSutWithZeroDelays();

        // Act
        var result = await sut.GetHealthAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.ServerError);
        result.Error.HttpStatusCode.Should().Be(500);
    }

    // ──────────────────────────────────────────────────────────────
    // Retry logic — 500 retries 3 times
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetHealthAsync_WhenServerReturns500_RetriesThreeTimes()
    {
        // Arrange
        _handler.SetupResponse("GET", "/global/health", HttpStatusCode.InternalServerError, "error");
        var sut = CreateSutWithZeroDelays();

        // Act
        await sut.GetHealthAsync();

        // Assert
        _handler.CallCount.TryGetValue("GET /global/health", out var count);
        count.Should().Be(3);
    }

    [Fact]
    public async Task GetHealthAsync_WhenServerReturns500_AfterThreeRetries_ReturnsServerError()
    {
        // Arrange
        _handler.SetupResponse("GET", "/global/health", HttpStatusCode.InternalServerError, "error");
        var sut = CreateSutWithZeroDelays();

        // Act
        var result = await sut.GetHealthAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(ErrorKind.ServerError);
    }

    [Fact]
    public async Task GetHealthAsync_WhenServerReturns401_DoesNotRetry()
    {
        // Arrange
        _handler.SetupResponse("GET", "/global/health", HttpStatusCode.Unauthorized, "");
        var sut = CreateSut();

        // Act
        await sut.GetHealthAsync();

        // Assert
        _handler.CallCount.TryGetValue("GET /global/health", out var count);
        count.Should().Be(1);
    }

    [Fact]
    public async Task GetHealthAsync_WhenNetworkUnreachable_RetriesThreeTimes()
    {
        // Arrange
        _handler.SetupException("GET", "/global/health");
        var sut = CreateSutWithZeroDelays();

        // Act
        await sut.GetHealthAsync();

        // Assert
        _handler.CallCount.TryGetValue("GET /global/health", out var count);
        count.Should().Be(3);
    }

    // ──────────────────────────────────────────────────────────────
    // ConnectionStatus transitions during retry
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetHealthAsync_WhenServerReturns500_SetsConnectionStatusConnectingDuringRetry()
    {
        // Arrange
        _handler.SetupResponse("GET", "/global/health", HttpStatusCode.InternalServerError, "error");
        var sut = CreateSutWithZeroDelays();

        // Act
        await sut.GetHealthAsync();

        // Assert — SetConnectionStatus(Connecting) must have been called at least once
        // (once per retry after the first attempt)
        _connectionManager.Received().SetConnectionStatus(ServerConnectionStatus.Connecting);
    }

    [Fact]
    public async Task GetHealthAsync_WhenAllRetriesExhausted_SetsConnectionStatusError()
    {
        // Arrange
        _handler.SetupResponse("GET", "/global/health", HttpStatusCode.InternalServerError, "error");
        var sut = CreateSutWithZeroDelays();

        // Act
        await sut.GetHealthAsync();

        // Assert — SetConnectionStatus(Error) must have been called after all retries fail
        _connectionManager.Received(1).SetConnectionStatus(ServerConnectionStatus.Error);
    }

    // ──────────────────────────────────────────────────────────────
    // Waiting state
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetHealthAsync_WhenRequestInFlight_IsWaitingForServerIsTrue()
    {
        // Arrange
        var pauseSource = new TaskCompletionSource<bool>();
        _handler.PauseSource = pauseSource;
        _handler.SetupResponse("GET", "/global/health", HttpStatusCode.OK,
            """{"healthy":true,"version":"1.0.0"}""");
        var sut = CreateSut();

        bool? capturedWaitingState = null;

        // Act — start the request but don't await it yet
        var requestTask = sut.GetHealthAsync();

        // Give the async machinery a moment to reach the HTTP call
        await Task.Delay(50);
        capturedWaitingState = sut.IsWaitingForServer;

        // Complete the paused response
        pauseSource.SetResult(true);
        await requestTask;

        // Assert
        capturedWaitingState.Should().BeTrue();
    }

    [Fact]
    public async Task GetHealthAsync_AfterRequestCompletes_IsWaitingForServerIsFalse()
    {
        // Arrange
        _handler.SetupResponse("GET", "/global/health", HttpStatusCode.OK,
            """{"healthy":true,"version":"1.0.0"}""");
        var sut = CreateSut();

        // Act
        await sut.GetHealthAsync();

        // Assert
        sut.IsWaitingForServer.Should().BeFalse();
    }

    [Fact]
    public async Task GetHealthAsync_AfterRequestFails_IsWaitingForServerIsFalse()
    {
        // Arrange
        // Use 401 (no retry) so the test completes immediately
        _handler.SetupResponse("GET", "/global/health", HttpStatusCode.Unauthorized, "");
        var sut = CreateSut();

        // Act
        await sut.GetHealthAsync();

        // Assert
        sut.IsWaitingForServer.Should().BeFalse();
    }

    // ──────────────────────────────────────────────────────────────
    // Auth header injection
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetHealthAsync_WhenBasicAuthConfigured_RequestIncludesAuthorizationHeader()
    {
        // Arrange
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("user:secret"));
        _connectionManager.GetBasicAuthHeaderAsync(Arg.Any<CancellationToken>())
            .Returns($"Basic {encoded}");
        _handler.SetupResponse("GET", "/global/health", HttpStatusCode.OK,
            """{"healthy":true,"version":"1.0.0"}""");
        var sut = CreateSut();

        // Act
        await sut.GetHealthAsync();

        // Assert
        _handler.LastRequest.Should().NotBeNull();
        _handler.LastRequest!.Headers.Authorization.Should().NotBeNull();
        _handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Basic");
        _handler.LastRequest.Headers.Authorization.Parameter.Should().Be(encoded);
    }

    [Fact]
    public async Task GetHealthAsync_WhenNoAuth_RequestDoesNotIncludeAuthorizationHeader()
    {
        // Arrange
        _connectionManager.GetBasicAuthHeaderAsync(Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _handler.SetupResponse("GET", "/global/health", HttpStatusCode.OK,
            """{"healthy":true,"version":"1.0.0"}""");
        var sut = CreateSut();

        // Act
        await sut.GetHealthAsync();

        // Assert
        _handler.LastRequest.Should().NotBeNull();
        _handler.LastRequest!.Headers.Authorization.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────
    // SSE — SubscribeToEventsAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SubscribeToEventsAsync_WhenServerSendsEvents_YieldsCorrectEventDtos()
    {
        // Arrange
        const string sseBody =
            "event: server.connected\r\n" +
            "data: {}\r\n" +
            "\r\n" +
            "event: session.created\r\n" +
            "id: evt-1\r\n" +
            "data: {\"sessionID\":\"abc\"}\r\n" +
            "\r\n";

        var sseContent = new StringContent(sseBody, Encoding.UTF8, "text/event-stream");
        var sseResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = sseContent };

        // Override the handler to return an SSE response for the event endpoint.
        // SubscribeToEventsAsync uses "opencode-sse" (infinite timeout, no resilience pipeline).
        var sseHandler = new SseHttpMessageHandler(sseResponse);
        _httpClientFactory.CreateClient("opencode-sse").Returns(new HttpClient(sseHandler));

        var sut = CreateSut();
        using var cts = new CancellationTokenSource();

        // Act
        var events = new List<OpencodeEventDto>();
        await foreach (var evt in sut.SubscribeToEventsAsync(cts.Token))
        {
            events.Add(evt);
        }

        // Assert
        events.Should().HaveCount(2);
        events[0].EventType.Should().Be("server.connected");
        events[1].EventType.Should().Be("session.created");
        events[1].EventId.Should().Be("evt-1");
    }

    [Fact]
    public async Task SubscribeToEventsAsync_WhenCancellationRequested_CompletesWithoutException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Use a pipe to control the SSE stream from the test
        var pipe = new System.IO.Pipelines.Pipe();
        var pipeContent = new PipeReaderHttpContent(pipe.Reader);
        var sseResponse = new HttpResponseMessage(HttpStatusCode.OK) { Content = pipeContent };

        var sseHandler = new SseHttpMessageHandler(sseResponse);
        _httpClientFactory.CreateClient("opencode-sse").Returns(new HttpClient(sseHandler));

        var sut = CreateSut();

        // Write one event to the pipe, then cancel
        var writeTask = Task.Run(async () =>
        {
            var bytes = Encoding.UTF8.GetBytes("event: server.connected\r\ndata: {}\r\n\r\n");
            await pipe.Writer.WriteAsync(bytes);
            await pipe.Writer.FlushAsync();
            // Cancel the token — the reader should stop
            cts.Cancel();
            pipe.Writer.Complete();
        });

        // Act
        var act = async () =>
        {
            await foreach (var evt in sut.SubscribeToEventsAsync(cts.Token))
            {
                // Consume events — the loop should exit cleanly after cancellation
            }
        };

        // Assert
        await act.Should().NotThrowAsync();
        await writeTask;
    }
}

/// <summary>
/// A minimal <see cref="HttpMessageHandler"/> that returns a pre-built response for SSE tests.
/// </summary>
internal sealed class SseHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage _response;

    public SseHttpMessageHandler(HttpResponseMessage response)
        => _response = response;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
        => Task.FromResult(_response);
}

/// <summary>
/// An <see cref="HttpContent"/> backed by a <see cref="System.IO.Pipelines.PipeReader"/>.
/// Allows the test to control exactly what data is written to the SSE stream.
/// </summary>
internal sealed class PipeReaderHttpContent : HttpContent
{
    private readonly System.IO.Pipelines.PipeReader _reader;

    public PipeReaderHttpContent(System.IO.Pipelines.PipeReader reader)
    {
        _reader = reader;
        Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
    }

    protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
    {
        await _reader.CopyToAsync(stream);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
