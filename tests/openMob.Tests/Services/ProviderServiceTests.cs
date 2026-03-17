using System.Text.Json;
using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;
using openMob.Core.Services;

namespace openMob.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ProviderService"/>.
/// </summary>
public sealed class ProviderServiceTests
{
    private readonly IOpencodeApiClient _apiClient;
    private readonly ProviderService _sut;

    public ProviderServiceTests()
    {
        _apiClient = Substitute.For<IOpencodeApiClient>();
        _sut = new ProviderService(_apiClient);
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private static ProviderDto BuildProvider(
        string id = "anthropic",
        string name = "Anthropic",
        string? key = null)
    {
        return new ProviderDto(
            Id: id, Name: name, Source: "config",
            Env: new List<string> { "ANTHROPIC_API_KEY" }, Key: key,
            Options: default, Models: JsonDocument.Parse("{}").RootElement);
    }

    // ─── GetProvidersAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetProvidersAsync_WhenApiReturnsSuccess_ReturnsProviderList()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProvider(), BuildProvider("openai", "OpenAI") };
        _apiClient.GetProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ProviderListResponseDto>.Success(
                new ProviderListResponseDto(
                    All: providers,
                    Default: new Dictionary<string, string> { ["anthropic"] = "claude-opus-4-5" },
                    Connected: new List<string> { "anthropic" })));

        // Act
        var result = await _sut.GetProvidersAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetProvidersAsync_WhenApiReturnsFailure_ReturnsEmptyList()
    {
        // Arrange
        _apiClient.GetProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ProviderListResponseDto>.Failure(
                new OpencodeApiError(ErrorKind.ServerError, "Server error", 500, null)));

        // Act
        var result = await _sut.GetProvidersAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProvidersAsync_CallsApiClientExactlyOnce()
    {
        // Arrange
        _apiClient.GetProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ProviderListResponseDto>.Success(
                new ProviderListResponseDto(
                    All: new List<ProviderDto>(),
                    Default: new Dictionary<string, string>(),
                    Connected: new List<string>())));

        // Act
        await _sut.GetProvidersAsync();

        // Assert
        await _apiClient.Received(1).GetProvidersAsync(Arg.Any<CancellationToken>());
    }

    // ─── SetProviderAuthAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task SetProviderAuthAsync_WhenApiReturnsSuccess_ReturnsTrue()
    {
        // Arrange
        _apiClient.SetProviderAuthAsync("anthropic", Arg.Any<SetProviderAuthRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<bool>.Success(true));

        // Act
        var result = await _sut.SetProviderAuthAsync("anthropic", "sk-test-key");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SetProviderAuthAsync_WhenApiReturnsFailure_ReturnsFalse()
    {
        // Arrange
        _apiClient.SetProviderAuthAsync("anthropic", Arg.Any<SetProviderAuthRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<bool>.Failure(
                new OpencodeApiError(ErrorKind.ServerError, "Error", 500, null)));

        // Act
        var result = await _sut.SetProviderAuthAsync("anthropic", "sk-test-key");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetProviderAuthAsync_PassesCorrectProviderIdToApi()
    {
        // Arrange
        _apiClient.SetProviderAuthAsync(Arg.Any<string>(), Arg.Any<SetProviderAuthRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<bool>.Success(true));

        // Act
        await _sut.SetProviderAuthAsync("openai", "sk-key");

        // Assert
        await _apiClient.Received(1).SetProviderAuthAsync(
            "openai",
            Arg.Any<SetProviderAuthRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null, "key")]
    [InlineData("", "key")]
    [InlineData("id", null)]
    [InlineData("id", "")]
    public async Task SetProviderAuthAsync_WhenIdOrKeyIsNullOrWhitespace_ThrowsArgumentException(string? providerId, string? apiKey)
    {
        // Act
        var act = async () => await _sut.SetProviderAuthAsync(providerId!, apiKey!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── GetConfiguredProvidersAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetConfiguredProvidersAsync_WhenApiReturnsSuccess_ReturnsProviderList()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProvider(), BuildProvider("openai", "OpenAI") };
        _apiClient.GetConfigProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ConfigProvidersDto>.Success(
                new ConfigProvidersDto(
                    Providers: providers,
                    Default: new Dictionary<string, string> { ["anthropic"] = "claude-sonnet-4-6" })));

        // Act
        var result = await _sut.GetConfiguredProvidersAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(p => p.Id == "anthropic");
        result.Should().Contain(p => p.Id == "openai");
    }

    [Fact]
    public async Task GetConfiguredProvidersAsync_WhenApiReturnsFailure_ReturnsEmptyList()
    {
        // Arrange
        _apiClient.GetConfigProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ConfigProvidersDto>.Failure(
                new OpencodeApiError(ErrorKind.ServerError, "Server error", 500, null)));

        // Act
        var result = await _sut.GetConfiguredProvidersAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConfiguredProvidersAsync_WhenProvidersIsNull_ReturnsEmptyList()
    {
        // Arrange
        _apiClient.GetConfigProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ConfigProvidersDto>.Success(
                new ConfigProvidersDto(
                    Providers: null,
                    Default: new Dictionary<string, string> { ["anthropic"] = "claude-sonnet-4-6" })));

        // Act
        var result = await _sut.GetConfiguredProvidersAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConfiguredProvidersAsync_CallsApiClientExactlyOnce()
    {
        // Arrange
        _apiClient.GetConfigProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ConfigProvidersDto>.Success(
                new ConfigProvidersDto(
                    Providers: new List<ProviderDto>(),
                    Default: new Dictionary<string, string>())));

        // Act
        await _sut.GetConfiguredProvidersAsync();

        // Assert
        await _apiClient.Received(1).GetConfigProvidersAsync(Arg.Any<CancellationToken>());
    }

    // ─── HasAnyProviderConfiguredAsync ────────────────────────────────────────

    [Fact]
    public async Task HasAnyProviderConfiguredAsync_WhenConnectedListIsNonEmpty_ReturnsTrue()
    {
        // Arrange
        _apiClient.GetProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ProviderListResponseDto>.Success(
                new ProviderListResponseDto(
                    All: new List<ProviderDto> { BuildProvider() },
                    Default: new Dictionary<string, string> { ["anthropic"] = "claude-opus-4-5" },
                    Connected: new List<string> { "anthropic" })));

        // Act
        var result = await _sut.HasAnyProviderConfiguredAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasAnyProviderConfiguredAsync_WhenConnectedListIsEmpty_ReturnsFalse()
    {
        // Arrange
        _apiClient.GetProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ProviderListResponseDto>.Success(
                new ProviderListResponseDto(
                    All: new List<ProviderDto> { BuildProvider(), BuildProvider("openai", "OpenAI") },
                    Default: new Dictionary<string, string>(),
                    Connected: new List<string>())));

        // Act
        var result = await _sut.HasAnyProviderConfiguredAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasAnyProviderConfiguredAsync_WhenNoProviders_ReturnsFalse()
    {
        // Arrange
        _apiClient.GetProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ProviderListResponseDto>.Success(
                new ProviderListResponseDto(
                    All: new List<ProviderDto>(),
                    Default: new Dictionary<string, string>(),
                    Connected: new List<string>())));

        // Act
        var result = await _sut.HasAnyProviderConfiguredAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasAnyProviderConfiguredAsync_WhenApiReturnsFailure_ReturnsFalse()
    {
        // Arrange
        _apiClient.GetProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ProviderListResponseDto>.Failure(
                new OpencodeApiError(ErrorKind.ServerError, "Error", 500, null)));

        // Act
        var result = await _sut.HasAnyProviderConfiguredAsync();

        // Assert
        result.Should().BeFalse();
    }
}
