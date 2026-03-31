using System.Text.Json;
using NSubstitute.ExceptionExtensions;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Http.Dtos.Opencode.Requests;
using openMob.Core.Services;
using openMob.Core.ViewModels;
using openMob.Tests.Helpers;

namespace openMob.Tests.ViewModels;

// ─── ConfigDto.IsPermissionAllow unit tests ───────────────────────────────────

/// <summary>
/// Unit tests for the <see cref="ConfigDto.IsPermissionAllow"/> computed helper property.
/// </summary>
public sealed class ConfigDtoIsPermissionAllowTests
{
    [Theory]
    [InlineData("allow",  true)]
    [InlineData("ask",    false)]
    [InlineData("deny",   false)]
    [InlineData("ALLOW",  false)]   // case-sensitive
    public void IsPermissionAllow_WhenPermissionIsString_ReturnsExpectedValue(
        string permissionValue, bool expected)
    {
        // Arrange
        var dto = MakeConfigDto(permissionValue);

        // Act
        var result = dto.IsPermissionAllow;

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsPermissionAllow_WhenPermissionIsNull_ReturnsFalse()
    {
        // Arrange
        var dto = MakeConfigDto(null);

        // Act
        var result = dto.IsPermissionAllow;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsPermissionAllow_WhenPermissionIsObject_ReturnsFalse()
    {
        // Arrange — permission is a JSON object, not a string
        var permissionElement = JsonSerializer.SerializeToElement(new { bash = "allow" });
        var dto = new ConfigDto(
            Theme: null, LogLevel: null, Model: null, SmallModel: null,
            Username: null, Share: null, Autoupdate: null, Snapshot: null,
            Keybinds: null, Tui: null, Command: null, Agent: null,
            Provider: null, Mcp: null, Lsp: null, Formatter: null,
            Permission: permissionElement,
            Tools: null, Experimental: null, Watcher: null);

        // Act
        var result = dto.IsPermissionAllow;

        // Assert
        result.Should().BeFalse();
    }

    // ─── Shared helper ────────────────────────────────────────────────────────

    private static ConfigDto MakeConfigDto(string? permissionValue)
    {
        JsonElement? permissionElement = permissionValue is null
            ? null
            : JsonSerializer.SerializeToElement(permissionValue);

        return new ConfigDto(
            Theme: null, LogLevel: null, Model: null, SmallModel: null,
            Username: null, Share: null, Autoupdate: null, Snapshot: null,
            Keybinds: null, Tui: null, Command: null, Agent: null,
            Provider: null, Mcp: null, Lsp: null, Formatter: null,
            Permission: permissionElement,
            Tools: null, Experimental: null, Watcher: null);
    }
}

// ─── ServerDetailViewModel — Auto-Approve tests ───────────────────────────────

/// <summary>
/// Unit tests for the server-side auto-approve feature in <see cref="ServerDetailViewModel"/>.
/// Covers <c>LoadAutoApproveConfigAsync</c> (called from <c>InitialiseAsync</c> in Edit mode)
/// and <c>ToggleServerAutoApproveCommand</c> happy paths, failure/rollback paths, and
/// <c>CanExecute</c> guards.
/// </summary>
public sealed class ServerDetailViewModelAutoApproveTests
{
    private readonly IServerConnectionRepository _serverConnectionRepository;
    private readonly IServerCredentialStore _credentialStore;
    private readonly IOpencodeConnectionManager _connectionManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;
    private readonly IProviderService _providerService;
    private readonly IOpencodeApiClient _apiClient;

    public ServerDetailViewModelAutoApproveTests()
    {
        _serverConnectionRepository = Substitute.For<IServerConnectionRepository>();
        _credentialStore = Substitute.For<IServerCredentialStore>();
        _connectionManager = Substitute.For<IOpencodeConnectionManager>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _navigationService = Substitute.For<INavigationService>();
        _popupService = Substitute.For<IAppPopupService>();
        _providerService = Substitute.For<IProviderService>();
        _apiClient = Substitute.For<IOpencodeApiClient>();
    }

    private ServerDetailViewModel CreateViewModel() => new(
        _serverConnectionRepository,
        _credentialStore,
        _connectionManager,
        _httpClientFactory,
        _navigationService,
        _popupService,
        _providerService,
        _apiClient);

    // ─── Shared helpers ───────────────────────────────────────────────────────

    private static ConfigDto MakeConfigDto(string? permissionValue)
    {
        JsonElement? permissionElement = permissionValue is null
            ? null
            : JsonSerializer.SerializeToElement(permissionValue);

        return new ConfigDto(
            Theme: null, LogLevel: null, Model: null, SmallModel: null,
            Username: null, Share: null, Autoupdate: null, Snapshot: null,
            Keybinds: null, Tui: null, Command: null, Agent: null,
            Provider: null, Mcp: null, Lsp: null, Formatter: null,
            Permission: permissionElement,
            Tools: null, Experimental: null, Watcher: null);
    }

    private static ServerConnectionDto MakeServerDto(string id = "server-1") => new(
        Id: id, Name: "Test Server", Host: "localhost", Port: 4096,
        Username: null, IsActive: true, DiscoveredViaMdns: false,
        UseHttps: false, CreatedAt: DateTime.UtcNow, UpdatedAt: DateTime.UtcNow,
        HasPassword: false, DefaultModelId: null);

    // ─── Group 2 — InitialiseAsync in Edit mode — config load ─────────────────

    [Fact]
    public async Task InitialiseAsync_EditMode_WhenConfigPermissionIsAllow_SetsIsServerAutoApproveEnabledTrue()
    {
        // Arrange
        var serverDto = MakeServerDto("server-1");
        _serverConnectionRepository
            .GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(serverDto);

        var configDto = MakeConfigDto("allow");
        _apiClient
            .GetConfigAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ConfigDto>.Success(configDto));

        var sut = CreateViewModel();

        // Act
        await sut.InitialiseAsync("server-1");

        // Assert
        sut.IsServerAutoApproveEnabled.Should().BeTrue();
        sut.IsAutoApproveConfigLoaded.Should().BeTrue();
        sut.AutoApproveErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task InitialiseAsync_EditMode_WhenConfigPermissionIsAsk_SetsIsServerAutoApproveEnabledFalse()
    {
        // Arrange
        var serverDto = MakeServerDto("server-1");
        _serverConnectionRepository
            .GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(serverDto);

        var configDto = MakeConfigDto("ask");
        _apiClient
            .GetConfigAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ConfigDto>.Success(configDto));

        var sut = CreateViewModel();

        // Act
        await sut.InitialiseAsync("server-1");

        // Assert
        sut.IsServerAutoApproveEnabled.Should().BeFalse();
        sut.IsAutoApproveConfigLoaded.Should().BeTrue();
        sut.AutoApproveErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task InitialiseAsync_EditMode_WhenGetConfigFails_SetsErrorMessageAndDisablesToggle()
    {
        // Arrange
        var serverDto = MakeServerDto("server-1");
        _serverConnectionRepository
            .GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(serverDto);

        var error = new OpencodeApiError(ErrorKind.ServerError, "Server error", 500, null);
        _apiClient
            .GetConfigAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ConfigDto>.Failure(error));

        var sut = CreateViewModel();

        // Act
        await sut.InitialiseAsync("server-1");

        // Assert
        sut.IsServerAutoApproveEnabled.Should().BeFalse();
        sut.IsAutoApproveConfigLoaded.Should().BeTrue();
        sut.AutoApproveErrorMessage.Should().Be("Impossibile leggere la configurazione del server.");
    }

    [Fact]
    public async Task InitialiseAsync_EditMode_WhenGetConfigThrows_SetsErrorMessageAndDisablesToggle()
    {
        // Arrange
        var serverDto = MakeServerDto("server-1");
        _serverConnectionRepository
            .GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(serverDto);

        _apiClient
            .GetConfigAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var sut = CreateViewModel();

        // Act
        await sut.InitialiseAsync("server-1");

        // Assert
        sut.IsServerAutoApproveEnabled.Should().BeFalse();
        sut.IsAutoApproveConfigLoaded.Should().BeTrue();
        sut.AutoApproveErrorMessage.Should().Be("Impossibile leggere la configurazione del server.");
    }

    [Fact]
    public async Task InitialiseAsync_AddMode_DoesNotCallGetConfig()
    {
        // Arrange
        var sut = CreateViewModel();

        // Act
        await sut.InitialiseAsync(null);

        // Assert
        await _apiClient.DidNotReceive().GetConfigAsync(Arg.Any<CancellationToken>());
        sut.IsAutoApproveConfigLoaded.Should().BeFalse();
    }

    // ─── Group 3 — ToggleServerAutoApproveCommand — happy paths ──────────────

    [Fact]
    public async Task ToggleServerAutoApproveCommand_WhenToggledOn_CallsUpdateConfigWithAllow()
    {
        // Arrange
        var configDto = MakeConfigDto("allow");
        _apiClient
            .UpdateConfigAsync(Arg.Any<UpdateConfigRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ConfigDto>.Success(configDto));

        var sut = CreateViewModel();
        sut.IsAutoApproveConfigLoaded = true;   // satisfy CanExecute
        sut.IsServerAutoApproveEnabled = false; // starting state: OFF

        UpdateConfigRequest? captured = null;
        _apiClient
            .UpdateConfigAsync(
                Arg.Do<UpdateConfigRequest>(r => captured = r),
                Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ConfigDto>.Success(configDto));

        // Act
        await sut.ToggleServerAutoApproveCommand.ExecuteAsync(null);

        // Assert
        await _apiClient.Received(1).UpdateConfigAsync(
            Arg.Any<UpdateConfigRequest>(),
            Arg.Any<CancellationToken>());
        captured.Should().NotBeNull();
        captured!.Config.GetProperty("permission").GetString().Should().Be("allow");
        sut.IsServerAutoApproveEnabled.Should().BeTrue();
        sut.AutoApproveErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ToggleServerAutoApproveCommand_WhenToggledOff_CallsUpdateConfigWithAsk()
    {
        // Arrange
        var configDto = MakeConfigDto("ask");
        _apiClient
            .UpdateConfigAsync(Arg.Any<UpdateConfigRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ConfigDto>.Success(configDto));

        var sut = CreateViewModel();
        sut.IsAutoApproveConfigLoaded = true;  // satisfy CanExecute
        sut.IsServerAutoApproveEnabled = true; // starting state: ON

        UpdateConfigRequest? captured = null;
        _apiClient
            .UpdateConfigAsync(
                Arg.Do<UpdateConfigRequest>(r => captured = r),
                Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ConfigDto>.Success(configDto));

        // Act
        await sut.ToggleServerAutoApproveCommand.ExecuteAsync(null);

        // Assert
        await _apiClient.Received(1).UpdateConfigAsync(
            Arg.Any<UpdateConfigRequest>(),
            Arg.Any<CancellationToken>());
        captured.Should().NotBeNull();
        captured!.Config.GetProperty("permission").GetString().Should().Be("ask");
        sut.IsServerAutoApproveEnabled.Should().BeFalse();
        sut.AutoApproveErrorMessage.Should().BeNull();
    }

    // ─── Group 4 — ToggleServerAutoApproveCommand — failure / rollback ────────

    [Fact]
    public async Task ToggleServerAutoApproveCommand_WhenUpdateConfigFails_RevertsIsServerAutoApproveEnabled()
    {
        // Arrange
        var error = new OpencodeApiError(ErrorKind.ServerError, "Server error", 500, null);
        _apiClient
            .UpdateConfigAsync(Arg.Any<UpdateConfigRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ConfigDto>.Failure(error));

        var sut = CreateViewModel();
        sut.IsAutoApproveConfigLoaded = true;
        sut.IsServerAutoApproveEnabled = false; // starting state: OFF

        // Act
        await sut.ToggleServerAutoApproveCommand.ExecuteAsync(null);

        // Assert — reverted back to false (the previous value)
        sut.IsServerAutoApproveEnabled.Should().BeFalse();
        sut.AutoApproveErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task ToggleServerAutoApproveCommand_WhenUpdateConfigFails_SetsAutoApproveErrorMessage()
    {
        // Arrange
        var error = new OpencodeApiError(ErrorKind.NetworkUnreachable, "Unreachable", null, null);
        _apiClient
            .UpdateConfigAsync(Arg.Any<UpdateConfigRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ConfigDto>.Failure(error));

        var sut = CreateViewModel();
        sut.IsAutoApproveConfigLoaded = true;
        sut.IsServerAutoApproveEnabled = true; // starting state: ON

        // Act
        await sut.ToggleServerAutoApproveCommand.ExecuteAsync(null);

        // Assert — reverted back to true (the previous value) and error message set
        sut.IsServerAutoApproveEnabled.Should().BeTrue();
        sut.AutoApproveErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ToggleServerAutoApproveCommand_WhenUpdateConfigThrows_RevertsIsServerAutoApproveEnabled()
    {
        // Arrange
        _apiClient
            .UpdateConfigAsync(Arg.Any<UpdateConfigRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var sut = CreateViewModel();
        sut.IsAutoApproveConfigLoaded = true;
        sut.IsServerAutoApproveEnabled = false; // starting state: OFF

        // Act
        await sut.ToggleServerAutoApproveCommand.ExecuteAsync(null);

        // Assert — reverted back to false (the previous value)
        sut.IsServerAutoApproveEnabled.Should().BeFalse();
        sut.AutoApproveErrorMessage.Should().NotBeNull();
    }

    [Fact]
    public async Task ToggleServerAutoApproveCommand_WhenUpdateConfigThrows_SetsAutoApproveErrorMessage()
    {
        // Arrange
        _apiClient
            .UpdateConfigAsync(Arg.Any<UpdateConfigRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var sut = CreateViewModel();
        sut.IsAutoApproveConfigLoaded = true;
        sut.IsServerAutoApproveEnabled = true; // starting state: ON

        // Act
        await sut.ToggleServerAutoApproveCommand.ExecuteAsync(null);

        // Assert — reverted back to true (the previous value) and error message set
        sut.IsServerAutoApproveEnabled.Should().BeTrue();
        sut.AutoApproveErrorMessage.Should().NotBeNullOrEmpty();
    }

    // ─── Group 5 — CanExecute guard ───────────────────────────────────────────

    [Fact]
    public void ToggleServerAutoApproveCommand_WhenConfigNotLoaded_CanExecuteIsFalse()
    {
        // Arrange
        var sut = CreateViewModel();
        sut.IsAutoApproveConfigLoaded = false; // config not yet loaded

        // Assert
        sut.ToggleServerAutoApproveCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void ToggleServerAutoApproveCommand_WhenConfigLoaded_CanExecuteIsTrue()
    {
        // Arrange
        var sut = CreateViewModel();
        sut.IsAutoApproveConfigLoaded = true;
        // IsTogglingAutoApprove defaults to false

        // Assert
        sut.ToggleServerAutoApproveCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task ToggleServerAutoApproveCommand_AfterSuccessfulToggle_LeavesIsTogglingAutoApproveFalse()
    {
        // Arrange
        var configDto = MakeConfigDto("allow");
        _apiClient
            .UpdateConfigAsync(Arg.Any<UpdateConfigRequest>(), Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<ConfigDto>.Success(configDto));

        var sut = CreateViewModel();
        sut.IsAutoApproveConfigLoaded = true;
        sut.IsServerAutoApproveEnabled = false;

        // Act
        await sut.ToggleServerAutoApproveCommand.ExecuteAsync(null);

        // Assert
        sut.IsTogglingAutoApprove.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleServerAutoApproveCommand_AfterFailedToggle_LeavesIsTogglingAutoApproveFalse()
    {
        // Arrange
        _apiClient
            .UpdateConfigAsync(Arg.Any<UpdateConfigRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var sut = CreateViewModel();
        sut.IsAutoApproveConfigLoaded = true;
        sut.IsServerAutoApproveEnabled = false;

        // Act
        await sut.ToggleServerAutoApproveCommand.ExecuteAsync(null);

        // Assert
        sut.IsTogglingAutoApprove.Should().BeFalse();
    }
}
