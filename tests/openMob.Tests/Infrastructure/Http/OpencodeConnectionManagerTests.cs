using System.Net;
using System.Net.Http;
using System.Text;
using openMob.Tests.Helpers;

namespace openMob.Tests.Infrastructure.Http;

/// <summary>
/// Unit tests for <see cref="OpencodeConnectionManager"/>.
/// </summary>
public sealed class OpencodeConnectionManagerTests
{
    private readonly IServerConnectionRepository _repository;
    private readonly IServerCredentialStore _credentialStore;
    private readonly MockHttpMessageHandler _handler;
    private readonly IHttpClientFactory _httpClientFactory;

    public OpencodeConnectionManagerTests()
    {
        _repository = Substitute.For<IServerConnectionRepository>();
        _credentialStore = Substitute.For<IServerCredentialStore>();
        _handler = new MockHttpMessageHandler();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _httpClientFactory.CreateClient("opencode").Returns(new HttpClient(_handler));
    }

    private OpencodeConnectionManager CreateSut()
        => new(_repository, _credentialStore, _httpClientFactory);

    // ──────────────────────────────────────────────────────────────
    // GetBaseUrlAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBaseUrlAsync_WhenActiveConnectionExists_ReturnsCorrectUrl()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto(
            id: "conn-1",
            host: "192.168.1.100",
            port: 4096,
            isActive: true);
        _repository.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(dto);
        var sut = CreateSut();

        // Act
        var result = await sut.GetBaseUrlAsync();

        // Assert
        result.Should().Be("http://192.168.1.100:4096");
    }

    [Fact]
    public async Task GetBaseUrlAsync_WhenNoActiveConnection_ReturnsNull()
    {
        // Arrange
        _repository.GetActiveAsync(Arg.Any<CancellationToken>()).Returns((ServerConnectionDto?)null);
        var sut = CreateSut();

        // Act
        var result = await sut.GetBaseUrlAsync();

        // Assert
        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────
    // GetBasicAuthHeaderAsync
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBasicAuthHeaderAsync_WhenUsernameIsNull_ReturnsNull()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto(
            id: "conn-1",
            username: null,
            isActive: true);
        _repository.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(dto);
        var sut = CreateSut();

        // Act
        var result = await sut.GetBasicAuthHeaderAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetBasicAuthHeaderAsync_WhenUsernameSetAndPasswordExists_ReturnsBase64Header()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto(
            id: "conn-1",
            username: "user",
            isActive: true);
        _repository.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(dto);
        _credentialStore.GetPasswordAsync("conn-1", Arg.Any<CancellationToken>()).Returns("secret");
        var sut = CreateSut();

        var expectedEncoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("user:secret"));
        var expectedHeader = $"Basic {expectedEncoded}";

        // Act
        var result = await sut.GetBasicAuthHeaderAsync();

        // Assert
        result.Should().Be(expectedHeader);
    }

    [Fact]
    public async Task GetBasicAuthHeaderAsync_WhenUsernameSetButNoPassword_ReturnsNull()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto(
            id: "conn-1",
            username: "user",
            isActive: true);
        _repository.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(dto);
        _credentialStore.GetPasswordAsync("conn-1", Arg.Any<CancellationToken>()).Returns((string?)null);
        var sut = CreateSut();

        // Act
        var result = await sut.GetBasicAuthHeaderAsync();

        // Assert
        result.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────
    // ConnectionStatus
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void ConnectionStatus_WhenInitialized_IsDisconnected()
    {
        // Act
        var sut = CreateSut();

        // Assert
        sut.ConnectionStatus.Should().Be(ServerConnectionStatus.Disconnected);
    }

    // ──────────────────────────────────────────────────────────────
    // StatusChanged event
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task StatusChanged_WhenConnectionStatusChanges_EventIsRaised()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto(
            id: "conn-1",
            host: "localhost",
            port: 4096,
            isActive: true);
        _repository.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(dto);
        _handler.SetupResponse("GET", "/global/health", System.Net.HttpStatusCode.OK,
            """{"healthy":true,"version":"1.0.0"}""");

        var sut = CreateSut();
        ServerConnectionStatus? raisedStatus = null;
        sut.StatusChanged += status => raisedStatus = status;

        // Act
        await sut.IsServerReachableAsync();

        // Assert
        raisedStatus.Should().NotBeNull();
        raisedStatus.Should().Be(ServerConnectionStatus.Connected);
    }

    // ──────────────────────────────────────────────────────────────
    // IsServerReachableAsync — error paths set ConnectionStatus = Error
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task IsServerReachableAsync_WhenServerReturnsNon2xx_SetsConnectionStatusError()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto(
            id: "conn-1",
            host: "localhost",
            port: 4096,
            isActive: true);
        _repository.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(dto);
        _handler.SetupResponse("GET", "/global/health", System.Net.HttpStatusCode.InternalServerError, "");
        var sut = CreateSut();

        // Act
        await sut.IsServerReachableAsync();

        // Assert
        sut.ConnectionStatus.Should().Be(ServerConnectionStatus.Error);
    }

    [Fact]
    public async Task IsServerReachableAsync_WhenNetworkThrows_SetsConnectionStatusError()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto(
            id: "conn-1",
            host: "localhost",
            port: 4096,
            isActive: true);
        _repository.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(dto);
        _handler.SetupException("GET", "/global/health");
        var sut = CreateSut();

        // Act
        await sut.IsServerReachableAsync();

        // Assert
        sut.ConnectionStatus.Should().Be(ServerConnectionStatus.Error);
    }
}
