using System.Net;
using NSubstitute.ExceptionExtensions;
using Zeroconf;

namespace openMob.Tests.Infrastructure.Discovery;

/// <summary>
/// Unit tests for <see cref="OpencodeDiscoveryService"/>.
/// All external dependencies (mDNS resolver, HTTP, repository) are fully mocked.
/// </summary>
public sealed class OpencodeDiscoveryServiceTests
{
    // -------------------------------------------------------------------------
    // Private fake implementations
    // -------------------------------------------------------------------------

    private sealed class FakeZeroconfHost : IZeroconfHost
    {
        public string DisplayName { get; init; } = string.Empty;
        public string IPAddress { get; init; } = string.Empty;
        public IReadOnlyDictionary<string, IService> Services { get; init; } =
            new Dictionary<string, IService>();
        public IReadOnlyList<string> IPAddresses { get; init; } = [];
        public string Id { get; init; } = string.Empty;
    }

    private sealed class FakeService : IService
    {
        public string Name { get; init; } = string.Empty;
        public string ServiceName { get; init; } = string.Empty;
        public int Port { get; init; }
        public int Ttl { get; init; }
        public IReadOnlyList<IReadOnlyDictionary<string, string>> Properties { get; init; } =
            new List<IReadOnlyDictionary<string, string>>();
    }

    private sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string content)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            });
        }
    }

    private sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => throw exception;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static HttpClient CreateHttpClient(HttpStatusCode statusCode, string content)
    {
        var handler = new FakeHttpMessageHandler(statusCode, content);
        return new HttpClient(handler);
    }

    private static HttpClient CreateThrowingHttpClient(Exception exception)
    {
        var handler = new ThrowingHttpMessageHandler(exception);
        return new HttpClient(handler);
    }

    private static FakeZeroconfHost BuildOpencodeHost(
        string displayName = "opencode-4096",
        string ipAddress = "192.168.1.10",
        int port = 4096)
    {
        var service = new FakeService { Name = displayName, Port = port };
        return new FakeZeroconfHost
        {
            DisplayName = displayName,
            IPAddress = ipAddress,
            Services = new Dictionary<string, IService> { [displayName] = service }
        };
    }

    private static DiscoveredServerDto BuildDiscoveredServer(
        string name = "opencode-4096",
        string host = "192.168.1.10",
        int port = 4096)
        => new(name, host, port, DateTimeOffset.UtcNow);

    // -------------------------------------------------------------------------
    // SUT factory
    // -------------------------------------------------------------------------

    private readonly IZeroconfResolver _resolver;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServerConnectionRepository _repository;
    private readonly OpencodeDiscoveryService _sut;

    public OpencodeDiscoveryServiceTests()
    {
        _resolver = Substitute.For<IZeroconfResolver>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _repository = Substitute.For<IServerConnectionRepository>();
        _sut = new OpencodeDiscoveryService(_resolver, _httpClientFactory, _repository);
    }

    // =========================================================================
    // ScanAsync
    // =========================================================================

    [Fact]
    public async Task ScanAsync_WhenNoServersOnNetwork_YieldsZeroResults()
    {
        // Arrange
        _resolver
            .ResolveAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IZeroconfHost>>([]));

        // Act
        var results = await _sut.ScanAsync().ToListAsync();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_WhenMatchingServerFound_YieldsDiscoveredServerDto()
    {
        // Arrange
        var host = BuildOpencodeHost("opencode-4096", "192.168.1.42", 4096);
        _resolver
            .ResolveAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IZeroconfHost>>([host]));

        // Act
        var results = await _sut.ScanAsync().ToListAsync();

        // Assert
        results.Should().ContainSingle();
        var dto = results[0];
        dto.Name.Should().Be("opencode-4096");
        dto.Host.Should().Be("192.168.1.42");
        dto.Port.Should().Be(4096);
    }

    [Fact]
    public async Task ScanAsync_WhenSameHostPortFoundTwice_YieldsOnlyOnce()
    {
        // Arrange — two hosts with identical IP + port
        var host1 = BuildOpencodeHost("opencode-4096", "192.168.1.10", 4096);
        var host2 = BuildOpencodeHost("opencode-4096", "192.168.1.10", 4096);
        _resolver
            .ResolveAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IZeroconfHost>>([host1, host2]));

        // Act
        var results = await _sut.ScanAsync().ToListAsync();

        // Assert
        results.Should().ContainSingle();
    }

    [Theory]
    [InlineData("myapp-service")]
    [InlineData("printer-http")]
    [InlineData("_http._tcp")]
    [InlineData("OPENCODE")]
    [InlineData("not-opencode-4096")]
    public async Task ScanAsync_WhenNonOpencodeServiceFound_IsFiltered(string displayName)
    {
        // Arrange
        var service = new FakeService { Name = displayName, Port = 4096 };
        var host = new FakeZeroconfHost
        {
            DisplayName = displayName,
            IPAddress = "192.168.1.99",
            Services = new Dictionary<string, IService> { [displayName] = service }
        };
        _resolver
            .ResolveAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IZeroconfHost>>([host]));

        // Act
        var results = await _sut.ScanAsync().ToListAsync();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_WhenMultipleOpencodeServersFound_YieldsAll()
    {
        // Arrange
        var host1 = BuildOpencodeHost("opencode-4096", "192.168.1.10", 4096);
        var host2 = BuildOpencodeHost("opencode-4097", "192.168.1.11", 4097);
        var host3 = BuildOpencodeHost("opencode-4098", "192.168.1.12", 4098);
        _resolver
            .ResolveAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IZeroconfHost>>([host1, host2, host3]));

        // Act
        var results = await _sut.ScanAsync().ToListAsync();

        // Assert
        results.Should().HaveCount(3);
        results.Should().Contain(r => r.Host == "192.168.1.10" && r.Port == 4096);
        results.Should().Contain(r => r.Host == "192.168.1.11" && r.Port == 4097);
        results.Should().Contain(r => r.Host == "192.168.1.12" && r.Port == 4098);
    }

    [Fact]
    public async Task ScanAsync_WhenCancelled_CompletesWithoutThrowing()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        _resolver
            .ResolveAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callInfo.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return Task.FromResult<IReadOnlyList<IZeroconfHost>>([]);
            });
        cts.Cancel();

        // Act
        var results = new List<DiscoveredServerDto>();
        var act = async () =>
        {
            await foreach (var item in _sut.ScanAsync(cts.Token))
                results.Add(item);
        };

        // Assert — no exception thrown and no results yielded after pre-cancellation
        await act.Should().NotThrowAsync();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanAsync_WhenResolverThrows_YieldsZeroResults()
    {
        // Arrange
        _resolver
            .ResolveAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("mDNS unavailable"));

        // Act
        var results = await _sut.ScanAsync().ToListAsync();

        // Assert
        results.Should().BeEmpty();
    }

    // =========================================================================
    // ValidateServerAsync
    // =========================================================================

    [Fact]
    public async Task ValidateServerAsync_WhenServerReturnsHealthyTrue_ReturnsTrue()
    {
        // Arrange
        var server = BuildDiscoveredServer();
        _httpClientFactory
            .CreateClient("discovery-probe")
            .Returns(CreateHttpClient(HttpStatusCode.OK, """{"healthy":true}"""));

        // Act
        var result = await _sut.ValidateServerAsync(server);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateServerAsync_WhenServerReturnsHealthyFalse_ReturnsFalse()
    {
        // Arrange
        var server = BuildDiscoveredServer();
        _httpClientFactory
            .CreateClient("discovery-probe")
            .Returns(CreateHttpClient(HttpStatusCode.OK, """{"healthy":false}"""));

        // Act
        var result = await _sut.ValidateServerAsync(server);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateServerAsync_WhenServerReturnsNonSuccessStatus_ReturnsFalse()
    {
        // Arrange
        var server = BuildDiscoveredServer();
        _httpClientFactory
            .CreateClient("discovery-probe")
            .Returns(CreateHttpClient(HttpStatusCode.ServiceUnavailable, string.Empty));

        // Act
        var result = await _sut.ValidateServerAsync(server);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateServerAsync_WhenHttpRequestExceptionThrown_ReturnsFalse()
    {
        // Arrange
        var server = BuildDiscoveredServer();
        _httpClientFactory
            .CreateClient("discovery-probe")
            .Returns(CreateThrowingHttpClient(new HttpRequestException("Connection refused")));

        // Act
        var result = await _sut.ValidateServerAsync(server);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateServerAsync_WhenTaskCancelledExceptionThrown_ReturnsFalse()
    {
        // Arrange
        var server = BuildDiscoveredServer();
        _httpClientFactory
            .CreateClient("discovery-probe")
            .Returns(CreateThrowingHttpClient(new TaskCanceledException("Request timed out")));

        // Act
        var result = await _sut.ValidateServerAsync(server);

        // Assert
        result.Should().BeFalse();
    }

    // =========================================================================
    // SaveDiscoveredServerAsync
    // =========================================================================

    [Fact]
    public async Task SaveDiscoveredServerAsync_WhenValidationPasses_CallsRepositoryWithMdnsFlagTrue()
    {
        // Arrange
        var server = BuildDiscoveredServer("opencode-4096", "192.168.1.10", 4096);
        _httpClientFactory
            .CreateClient("discovery-probe")
            .Returns(CreateHttpClient(HttpStatusCode.OK, """{"healthy":true}"""));
        _repository
            .AddAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<ServerConnectionDto>()));

        // Act
        await _sut.SaveDiscoveredServerAsync(server);

        // Assert
        await _repository.Received(1).AddAsync(
            Arg.Is<ServerConnectionDto>(dto => dto.DiscoveredViaMdns == true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveDiscoveredServerAsync_WhenValidationPasses_ReturnsCreatedDto()
    {
        // Arrange
        var server = BuildDiscoveredServer("opencode-4096", "192.168.1.10", 4096);
        var createdDto = Helpers.TestDataBuilder.CreateServerConnectionDto(
            id: "new-ulid-001",
            name: "opencode-4096",
            host: "192.168.1.10",
            port: 4096,
            discoveredViaMdns: true);
        _httpClientFactory
            .CreateClient("discovery-probe")
            .Returns(CreateHttpClient(HttpStatusCode.OK, """{"healthy":true}"""));
        _repository
            .AddAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(createdDto));

        // Act
        var result = await _sut.SaveDiscoveredServerAsync(server);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("new-ulid-001");
        result.DiscoveredViaMdns.Should().BeTrue();
    }

    [Fact]
    public async Task SaveDiscoveredServerAsync_WhenValidationFails_ReturnsNullAndDoesNotCallRepository()
    {
        // Arrange
        var server = BuildDiscoveredServer();
        _httpClientFactory
            .CreateClient("discovery-probe")
            .Returns(CreateHttpClient(HttpStatusCode.ServiceUnavailable, string.Empty));

        // Act
        var result = await _sut.SaveDiscoveredServerAsync(server);

        // Assert
        result.Should().BeNull();
        await _repository.DidNotReceive().AddAsync(
            Arg.Any<ServerConnectionDto>(),
            Arg.Any<CancellationToken>());
    }
}
