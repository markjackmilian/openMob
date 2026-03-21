using openMob.Core.Infrastructure.Http;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Infrastructure.Security;
using openMob.Core.Services;
using openMob.Core.ViewModels;
using openMob.Tests.Helpers;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="OnboardingViewModel"/>.
/// </summary>
public sealed class OnboardingViewModelTests
{
    private readonly IServerConnectionRepository _serverConnectionRepo;
    private readonly IServerCredentialStore _credentialStore;
    private readonly IOpencodeConnectionManager _connectionManager;
    private readonly IOpencodeApiClient _apiClient;
    private readonly IProviderService _providerService;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;
    private readonly OnboardingViewModel _sut;

    public OnboardingViewModelTests()
    {
        _serverConnectionRepo = Substitute.For<IServerConnectionRepository>();
        _credentialStore = Substitute.For<IServerCredentialStore>();
        _connectionManager = Substitute.For<IOpencodeConnectionManager>();
        _apiClient = Substitute.For<IOpencodeApiClient>();
        _providerService = Substitute.For<IProviderService>();
        _navigationService = Substitute.For<INavigationService>();
        _popupService = Substitute.For<IAppPopupService>();

        _sut = new OnboardingViewModel(
            _serverConnectionRepo,
            _credentialStore,
            _connectionManager,
            _apiClient,
            _providerService,
            _navigationService,
            _popupService);
    }

    // ─── Initial state ───────────────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesWithStep1()
    {
        // Assert
        _sut.CurrentStep.Should().Be(1);
    }

    [Fact]
    public void Constructor_TotalStepsIs4()
    {
        // Assert
        _sut.TotalSteps.Should().Be(4);
    }

    // ─── Progress ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 0.25)]
    [InlineData(2, 0.5)]
    [InlineData(3, 0.75)]
    [InlineData(4, 1.0)]
    public void Progress_ReturnsCorrectValueForStep(int step, double expectedProgress)
    {
        // Arrange — advance to the desired step
        for (var i = 1; i < step; i++)
        {
            _sut.CurrentStep = i + 1;
        }

        // Act
        var progress = _sut.Progress;

        // Assert
        progress.Should().BeApproximately(expectedProgress, 0.001);
    }

    // ─── CanGoNext ────────────────────────────────────────────────────────────

    [Fact]
    public void CanGoNext_Step1_IsTrue()
    {
        // Assert
        _sut.CanGoNext.Should().BeTrue();
    }

    [Fact]
    public void CanGoNext_Step2_WhenNotConnected_IsFalse()
    {
        // Arrange
        _sut.CurrentStep = 2;

        // Assert
        _sut.CanGoNext.Should().BeFalse();
    }

    [Fact]
    public void CanGoNext_Step2_WhenConnected_IsTrue()
    {
        // Arrange
        _sut.CurrentStep = 2;
        _sut.IsConnectionSuccessful = true;

        // Assert
        _sut.CanGoNext.Should().BeTrue();
    }

    [Fact]
    public void CanGoNext_Step3_WhenNoModelSelected_IsFalse()
    {
        // Arrange
        _sut.CurrentStep = 3;

        // Assert
        _sut.CanGoNext.Should().BeFalse();
    }

    [Fact]
    public void CanGoNext_Step3_WhenModelSelected_IsTrue()
    {
        // Arrange
        _sut.CurrentStep = 3;
        _sut.SelectedModelId = "anthropic/claude-3-opus";

        // Assert
        _sut.CanGoNext.Should().BeTrue();
    }

    // ─── CanGoBack ────────────────────────────────────────────────────────────

    [Fact]
    public void CanGoBack_Step1_IsFalse()
    {
        // Assert
        _sut.CanGoBack.Should().BeFalse();
    }

    [Fact]
    public void CanGoBack_Step2_IsTrue()
    {
        // Arrange
        _sut.CurrentStep = 2;

        // Assert
        _sut.CanGoBack.Should().BeTrue();
    }

    // ─── IsStepOptional ───────────────────────────────────────────────────────

    [Fact]
    public void IsStepOptional_Step2_IsTrue()
    {
        // Arrange
        _sut.CurrentStep = 2;

        // Assert
        _sut.IsStepOptional.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    public void IsStepOptional_NonStep2_IsFalse(int step)
    {
        // Arrange
        _sut.CurrentStep = step;

        // Assert
        _sut.IsStepOptional.Should().BeFalse();
    }

    // ─── NextStepCommand ──────────────────────────────────────────────────────

    [Fact]
    public async Task NextStepCommand_WhenOnStep1_AdvancesToStep2()
    {
        // Act
        await _sut.NextStepCommand.ExecuteAsync(null);

        // Assert
        _sut.CurrentStep.Should().Be(2);
    }

    [Fact]
    public async Task NextStepCommand_WhenOnStep2_AdvancesToStep3AndLoadsModels()
    {
        // Arrange
        _sut.CurrentStep = 2;
        _sut.IsConnectionSuccessful = true;
        _providerService.GetConfiguredProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProviderDto>());

        // Act
        await _sut.NextStepCommand.ExecuteAsync(null);

        // Assert
        _sut.CurrentStep.Should().Be(3);
        await _providerService.Received(1).GetConfiguredProvidersAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NextStepCommand_WhenOnStep3_AdvancesToStep4()
    {
        // Arrange
        _sut.CurrentStep = 3;
        _sut.SelectedModelId = "anthropic/claude-3-opus";

        // Act
        await _sut.NextStepCommand.ExecuteAsync(null);

        // Assert
        _sut.CurrentStep.Should().Be(4);
    }

    // ─── PreviousStepCommand ──────────────────────────────────────────────────

    [Fact]
    public void PreviousStepCommand_WhenOnStep2_DecrementsToStep1()
    {
        // Arrange
        _sut.CurrentStep = 2;

        // Act
        _sut.PreviousStepCommand.Execute(null);

        // Assert
        _sut.CurrentStep.Should().Be(1);
    }

    [Fact]
    public void PreviousStepCommand_WhenOnStep1_StaysAtStep1()
    {
        // Act
        _sut.PreviousStepCommand.Execute(null);

        // Assert
        _sut.CurrentStep.Should().Be(1);
    }

    // ─── CompleteOnboardingCommand ────────────────────────────────────────────

    [Fact]
    public async Task CompleteOnboardingCommand_NavigatesToChat()
    {
        // Act
        await _sut.CompleteOnboardingCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.Received(1).GoToAsync("//chat", Arg.Any<CancellationToken>());
    }

    // ─── NextStepCommand at step 5 completes onboarding ──────────────────────

    [Fact]
    public async Task NextStepCommand_WhenOnStep4_NavigatesToChat()
    {
        // Arrange
        _sut.CurrentStep = 4;

        // Act
        await _sut.NextStepCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.Received(1).GoToAsync("//chat", Arg.Any<CancellationToken>());
    }

    // ─── SkipStepCommand ──────────────────────────────────────────────────────

    [Fact]
    public async Task SkipStepCommand_WhenOnStep3_AdvancesToStep4()
    {
        // Arrange
        _sut.CurrentStep = 3;

        // Act
        await _sut.SkipStepCommand.ExecuteAsync(null);

        // Assert
        _sut.CurrentStep.Should().Be(4);
    }

    [Fact]
    public async Task SkipStepCommand_WhenOnStep4_NavigatesToChat()
    {
        // Arrange
        _sut.CurrentStep = 4;

        // Act
        await _sut.SkipStepCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.Received(1).GoToAsync("//chat", Arg.Any<CancellationToken>());
    }

    // ─── TestConnectionCommand ────────────────────────────────────────────────

    [Fact]
    public async Task TestConnectionCommand_WhenUrlIsEmpty_SetsConnectionStatusMessage()
    {
        // Arrange
        _sut.ServerUrl = "";

        // Act
        await _sut.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        _sut.ConnectionStatusMessage.Should().Contain("Please enter a server URL");
        _sut.IsConnectionTested.Should().BeTrue();
        _sut.IsConnectionSuccessful.Should().BeFalse();
    }

    [Fact]
    public async Task TestConnectionCommand_WhenUrlIsInvalid_SetsInvalidUrlMessage()
    {
        // Arrange
        _sut.ServerUrl = "not-a-url";

        // Act
        await _sut.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        _sut.ConnectionStatusMessage.Should().Contain("Invalid URL format");
        _sut.IsConnectionTested.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionCommand_WhenHealthCheckSucceeds_SetsConnectionSuccessful()
    {
        // Arrange
        _sut.ServerUrl = "http://192.168.1.100:4096";
        _sut.ServerToken = "test-token";

        var savedConnection = new ServerConnectionDto(
            "conn-1", "192.168.1.100:4096", "192.168.1.100", 4096, "opencode",
            true, false, false, DateTime.UtcNow, DateTime.UtcNow, true);

        _serverConnectionRepo.AddAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>())
            .Returns(savedConnection);
        _serverConnectionRepo.SetActiveAsync("conn-1", Arg.Any<CancellationToken>())
            .Returns(true);

        var healthDto = new HealthDto(Healthy: true, Version: "1.0.0");
        _apiClient.GetHealthAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<HealthDto>.Success(healthDto));

        // Act
        await _sut.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        _sut.IsConnectionSuccessful.Should().BeTrue();
        _sut.ConnectionStatusMessage.Should().Contain("Connected");
        _sut.ConnectionStatusMessage.Should().Contain("1.0.0");
    }

    [Fact]
    public async Task TestConnectionCommand_WhenHealthCheckFails_SetsConnectionFailed()
    {
        // Arrange
        _sut.ServerUrl = "http://192.168.1.100:4096";

        var savedConnection = new ServerConnectionDto(
            "conn-1", "192.168.1.100:4096", "192.168.1.100", 4096, "opencode",
            false, false, false, DateTime.UtcNow, DateTime.UtcNow, false);

        _serverConnectionRepo.AddAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>())
            .Returns(savedConnection);
        _serverConnectionRepo.SetActiveAsync("conn-1", Arg.Any<CancellationToken>())
            .Returns(true);

        _apiClient.GetHealthAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<HealthDto>.Failure(
                new OpencodeApiError(ErrorKind.NetworkUnreachable, "Connection refused", null, null)));

        // Act
        await _sut.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        _sut.IsConnectionSuccessful.Should().BeFalse();
        _sut.IsConnectionTested.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionCommand_WhenHealthCheckFails_DeletesConnection()
    {
        // Arrange
        _sut.ServerUrl = "http://192.168.1.100:4096";

        var savedConnection = new ServerConnectionDto(
            "conn-1", "192.168.1.100:4096", "192.168.1.100", 4096, "opencode",
            false, false, false, DateTime.UtcNow, DateTime.UtcNow, false);

        _serverConnectionRepo.AddAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>())
            .Returns(savedConnection);
        _serverConnectionRepo.SetActiveAsync("conn-1", Arg.Any<CancellationToken>())
            .Returns(true);

        _apiClient.GetHealthAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<HealthDto>.Failure(
                new OpencodeApiError(ErrorKind.NetworkUnreachable, "Connection refused", null, null)));

        // Act
        await _sut.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        await _serverConnectionRepo.Received(1).DeleteAsync("conn-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TestConnectionCommand_WhenSuccessful_SavesCredentials()
    {
        // Arrange
        _sut.ServerUrl = "http://192.168.1.100:4096";
        _sut.ServerToken = "my-secret-token";

        var savedConnection = new ServerConnectionDto(
            "conn-1", "192.168.1.100:4096", "192.168.1.100", 4096, "opencode",
            true, false, false, DateTime.UtcNow, DateTime.UtcNow, true);

        _serverConnectionRepo.AddAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>())
            .Returns(savedConnection);
        _serverConnectionRepo.SetActiveAsync("conn-1", Arg.Any<CancellationToken>())
            .Returns(true);

        var healthDto = new HealthDto(Healthy: true, Version: "1.0.0");
        _apiClient.GetHealthAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<HealthDto>.Success(healthDto));

        // Act
        await _sut.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        await _credentialStore.Received(1).SavePasswordAsync(
            "conn-1",
            "my-secret-token",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TestConnectionCommand_SetsIsTestingConnectionFalseAfterCompletion()
    {
        // Arrange
        _sut.ServerUrl = "";

        // Act
        await _sut.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        _sut.IsTestingConnection.Should().BeFalse();
    }

    [Fact]
    public async Task TestConnectionCommand_WhenConnectionFails_ShowsErrorPopup()
    {
        // Arrange
        _sut.ServerUrl = "http://192.168.1.100:4096";

        var savedConnection = new ServerConnectionDto(
            "conn-1", "192.168.1.100:4096", "192.168.1.100", 4096, "opencode",
            false, false, false, DateTime.UtcNow, DateTime.UtcNow, false);

        _serverConnectionRepo.AddAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>())
            .Returns(savedConnection);
        _serverConnectionRepo.SetActiveAsync("conn-1", Arg.Any<CancellationToken>())
            .Returns(true);

        _apiClient.GetHealthAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<HealthDto>.Failure(
                new OpencodeApiError(ErrorKind.NetworkUnreachable, "Connection refused", null, null)));

        // Act
        await _sut.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).ShowErrorAsync(
            "Connection Failed",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    // ─── TestConnectionCommand — UseHttps / Port extraction ──────────────────

    [Fact]
    public async Task TestConnectionCommand_WhenHttpsUrlWithDefaultPort_SavesUseHttpsTrue()
    {
        // Arrange
        _sut.ServerUrl = "https://3d6e-149-86-203-226.ngrok-free.app";

        var savedConnection = TestDataBuilder.CreateServerConnectionDto(id: "conn-1");
        _serverConnectionRepo.AddAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>())
            .Returns(savedConnection);
        _serverConnectionRepo.SetActiveAsync("conn-1", Arg.Any<CancellationToken>())
            .Returns(true);

        var healthDto = new HealthDto(Healthy: true, Version: "1.0.0");
        _apiClient.GetHealthAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<HealthDto>.Success(healthDto));

        // Act
        await _sut.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        await _serverConnectionRepo.Received(1).AddAsync(
            Arg.Is<ServerConnectionDto>(dto => dto.UseHttps == true && dto.Port == 443),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TestConnectionCommand_WhenHttpUrlWithExplicitPort_SavesUseHttpsFalse()
    {
        // Arrange
        _sut.ServerUrl = "http://192.168.1.100:4096";

        var savedConnection = TestDataBuilder.CreateServerConnectionDto(id: "conn-1");
        _serverConnectionRepo.AddAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>())
            .Returns(savedConnection);
        _serverConnectionRepo.SetActiveAsync("conn-1", Arg.Any<CancellationToken>())
            .Returns(true);

        var healthDto = new HealthDto(Healthy: true, Version: "1.0.0");
        _apiClient.GetHealthAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<HealthDto>.Success(healthDto));

        // Act
        await _sut.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        await _serverConnectionRepo.Received(1).AddAsync(
            Arg.Is<ServerConnectionDto>(dto => dto.UseHttps == false && dto.Port == 4096),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TestConnectionCommand_WhenHttpsUrlWithCustomPort_SavesCorrectPortAndUseHttpsTrue()
    {
        // Arrange
        _sut.ServerUrl = "https://myserver.com:8443";

        var savedConnection = TestDataBuilder.CreateServerConnectionDto(id: "conn-1");
        _serverConnectionRepo.AddAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>())
            .Returns(savedConnection);
        _serverConnectionRepo.SetActiveAsync("conn-1", Arg.Any<CancellationToken>())
            .Returns(true);

        var healthDto = new HealthDto(Healthy: true, Version: "1.0.0");
        _apiClient.GetHealthAsync(Arg.Any<CancellationToken>())
            .Returns(OpencodeResult<HealthDto>.Success(healthDto));

        // Act
        await _sut.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        await _serverConnectionRepo.Received(1).AddAsync(
            Arg.Is<ServerConnectionDto>(dto => dto.UseHttps == true && dto.Port == 8443),
            Arg.Any<CancellationToken>());
    }
}
