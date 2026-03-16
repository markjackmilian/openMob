using System.Net;
using openMob.Core.Services;
using openMob.Core.ViewModels;
using openMob.Tests.Helpers;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="ServerDetailViewModel"/>.
/// </summary>
public sealed class ServerDetailViewModelTests
{
    private readonly IServerConnectionRepository _repository;
    private readonly IServerCredentialStore _credentialStore;
    private readonly IOpencodeConnectionManager _connectionManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;

    public ServerDetailViewModelTests()
    {
        _repository = Substitute.For<IServerConnectionRepository>();
        _credentialStore = Substitute.For<IServerCredentialStore>();
        _connectionManager = Substitute.For<IOpencodeConnectionManager>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _navigationService = Substitute.For<INavigationService>();
        _popupService = Substitute.For<IAppPopupService>();
    }

    private ServerDetailViewModel CreateSut()
        => new(_repository, _credentialStore, _connectionManager, _httpClientFactory, _navigationService, _popupService);

    // ─── Fake HTTP helpers ────────────────────────────────────────────────────

    private static HttpClient CreateFakeHttpClient(HttpStatusCode statusCode, string responseBody)
    {
        var handler = new FakeHttpMessageHandler(statusCode, responseBody);
        return new HttpClient(handler);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody)
            });
        }
    }

    private sealed class TimeoutHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => throw new OperationCanceledException();
    }

    private sealed class NetworkErrorHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => throw new HttpRequestException("Network unreachable");
    }

    // ─── Constructor — null guards ────────────────────────────────────────────

    [Fact]
    public void Constructor_WhenRepositoryIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ServerDetailViewModel(null!, _credentialStore, _connectionManager, _httpClientFactory, _navigationService, _popupService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("serverConnectionRepository");
    }

    [Fact]
    public void Constructor_WhenCredentialStoreIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ServerDetailViewModel(_repository, null!, _connectionManager, _httpClientFactory, _navigationService, _popupService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("credentialStore");
    }

    [Fact]
    public void Constructor_WhenConnectionManagerIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ServerDetailViewModel(_repository, _credentialStore, null!, _httpClientFactory, _navigationService, _popupService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("connectionManager");
    }

    [Fact]
    public void Constructor_WhenHttpClientFactoryIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ServerDetailViewModel(_repository, _credentialStore, _connectionManager, null!, _navigationService, _popupService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClientFactory");
    }

    [Fact]
    public void Constructor_WhenNavigationServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ServerDetailViewModel(_repository, _credentialStore, _connectionManager, _httpClientFactory, null!, _popupService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("navigationService");
    }

    [Fact]
    public void Constructor_WhenPopupServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ServerDetailViewModel(_repository, _credentialStore, _connectionManager, _httpClientFactory, _navigationService, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("popupService");
    }

    // ─── InitialiseAsync — Add mode (null serverId) ───────────────────────────

    [Fact]
    public async Task InitialiseAsync_WhenServerIdIsNull_SetsAddMode()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.InitialiseAsync(null);

        // Assert
        sut.IsEditMode.Should().BeFalse();
        sut.IsSaved.Should().BeFalse();
        sut.Name.Should().BeEmpty();
        sut.Url.Should().BeEmpty();
    }

    // ─── InitialiseAsync — Edit mode ──────────────────────────────────────────

    [Fact]
    public async Task InitialiseAsync_WhenServerIdIsValid_LoadsFromRepositoryAndSetsEditMode()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto("id1", name: "My Server", host: "192.168.1.10", port: 4096, useHttps: false);
        _repository.GetByIdAsync("id1", Arg.Any<CancellationToken>())
            .Returns(dto);

        var sut = CreateSut();

        // Act
        await sut.InitialiseAsync("id1");

        // Assert
        sut.IsEditMode.Should().BeTrue();
        sut.IsSaved.Should().BeTrue();
        sut.Name.Should().Be("My Server");
        sut.Url.Should().Be("http://192.168.1.10:4096");
    }

    [Fact]
    public async Task InitialiseAsync_WhenHasPasswordIsTrue_SetsPasswordPlaceholderToSavedMessage()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto("id1", hasPassword: true);
        _repository.GetByIdAsync("id1", Arg.Any<CancellationToken>())
            .Returns(dto);

        var sut = CreateSut();

        // Act
        await sut.InitialiseAsync("id1");

        // Assert
        sut.PasswordPlaceholder.Should().Be("Password saved — leave empty to keep unchanged");
    }

    [Fact]
    public async Task InitialiseAsync_WhenHasPasswordIsFalse_SetsDefaultPasswordPlaceholder()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto("id1", hasPassword: false);
        _repository.GetByIdAsync("id1", Arg.Any<CancellationToken>())
            .Returns(dto);

        var sut = CreateSut();

        // Act
        await sut.InitialiseAsync("id1");

        // Assert
        sut.PasswordPlaceholder.Should().Be("Leave empty if not required");
    }

    // ─── InitialiseAsync — discovered server pre-population ──────────────────

    [Fact]
    public async Task InitialiseAsync_WhenDiscoveredParamsProvided_PrePopulatesNameAndUrl()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.InitialiseAsync(null, "192.168.1.50", 4096, "opencode-4096");

        // Assert
        sut.Name.Should().Be("opencode-4096");
        sut.Url.Should().Be("http://192.168.1.50:4096");
        sut.IsEditMode.Should().BeFalse();
    }

    // ─── SaveCommand — validation ─────────────────────────────────────────────

    [Fact]
    public async Task SaveCommand_WhenNameIsEmpty_SetsValidationError_DoesNotCallRepository()
    {
        // Arrange
        var sut = CreateSut();
        sut.Name = "";
        sut.Url = "http://192.168.1.10:4096";

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        sut.ValidationError.Should().NotBeNullOrEmpty();
        await _repository.DidNotReceive().AddAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveCommand_WhenUrlIsInvalid_SetsValidationError_DoesNotCallRepository()
    {
        // Arrange
        var sut = CreateSut();
        sut.Name = "Test";
        sut.Url = "not-a-url";

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        sut.ValidationError.Should().NotBeNullOrEmpty();
        await _repository.DidNotReceive().AddAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveCommand_WhenUrlHasInvalidScheme_SetsValidationError()
    {
        // Arrange
        var sut = CreateSut();
        sut.Name = "Test";
        sut.Url = "ftp://192.168.1.10:4096";

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        sut.ValidationError.Should().NotBeNullOrEmpty();
    }

    // ─── SaveCommand — Add mode ───────────────────────────────────────────────

    [Fact]
    public async Task SaveCommand_WhenAddModeAndValidInputs_CallsAddAsyncAndNavigatesBack()
    {
        // Arrange
        var sut = CreateSut();
        sut.Name = "My Server";
        sut.Url = "http://192.168.1.10:4096";
        sut.Password = "";

        _repository.AddAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>())
            .Returns(TestDataBuilder.CreateServerConnectionDto("new-id"));

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        await _repository.Received(1).AddAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>());
        await _navigationService.Received(1).PopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveCommand_WhenAddModeAndPasswordProvided_CallsSavePasswordAsync()
    {
        // Arrange
        var sut = CreateSut();
        sut.Name = "My Server";
        sut.Url = "http://192.168.1.10:4096";
        sut.Password = "secret";

        _repository.AddAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>())
            .Returns(TestDataBuilder.CreateServerConnectionDto("new-id"));

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        await _credentialStore.Received(1).SavePasswordAsync(
            "new-id",
            "secret",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveCommand_WhenAddModeAndNoPassword_DoesNotCallSavePasswordAsync()
    {
        // Arrange
        var sut = CreateSut();
        sut.Name = "My Server";
        sut.Url = "http://192.168.1.10:4096";
        sut.Password = "";

        _repository.AddAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>())
            .Returns(TestDataBuilder.CreateServerConnectionDto("new-id"));

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        await _credentialStore.DidNotReceive().SavePasswordAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveCommand_WhenAddModeAndValidInputs_SetsIsSavedTrueAndIsEditModeTrue()
    {
        // Arrange
        var sut = CreateSut();
        sut.Name = "My Server";
        sut.Url = "http://192.168.1.10:4096";
        sut.Password = "";

        _repository.AddAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>())
            .Returns(TestDataBuilder.CreateServerConnectionDto("new-id"));

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        sut.IsSaved.Should().BeTrue();
        sut.IsEditMode.Should().BeTrue();
    }

    // ─── SaveCommand — Edit mode ──────────────────────────────────────────────

    [Fact]
    public async Task SaveCommand_WhenEditModeAndValidInputs_CallsUpdateAsyncAndNavigatesBack()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto("id1", name: "My Server", host: "192.168.1.10", port: 4096, useHttps: false);
        _repository.GetByIdAsync("id1", Arg.Any<CancellationToken>())
            .Returns(dto);
        _repository.UpdateAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        var sut = CreateSut();
        await sut.InitialiseAsync("id1");

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        await _repository.Received(1).UpdateAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>());
        await _navigationService.Received(1).PopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveCommand_WhenEditModeAndPasswordChanged_CallsSavePasswordAsync()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto("id1", name: "My Server", host: "192.168.1.10", port: 4096, hasPassword: false);
        _repository.GetByIdAsync("id1", Arg.Any<CancellationToken>())
            .Returns(dto);
        _repository.UpdateAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        var sut = CreateSut();
        await sut.InitialiseAsync("id1");
        sut.Password = "newpassword";

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        await _credentialStore.Received(1).SavePasswordAsync(
            Arg.Any<string>(),
            "newpassword",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveCommand_WhenEditModeAndPasswordClearedAndHadPassword_CallsDeletePasswordAsync()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto("id1", name: "My Server", host: "192.168.1.10", port: 4096, hasPassword: true);
        _repository.GetByIdAsync("id1", Arg.Any<CancellationToken>())
            .Returns(dto);
        _repository.UpdateAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        var sut = CreateSut();
        await sut.InitialiseAsync("id1");
        sut.Password = ""; // cleared

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        await _credentialStore.Received(1).DeletePasswordAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveCommand_WhenEditModeAndPasswordEmptyAndNeverHadPassword_DoesNotCallCredentialStore()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto("id1", name: "My Server", host: "192.168.1.10", port: 4096, hasPassword: false);
        _repository.GetByIdAsync("id1", Arg.Any<CancellationToken>())
            .Returns(dto);
        _repository.UpdateAsync(Arg.Any<ServerConnectionDto>(), Arg.Any<CancellationToken>())
            .Returns(dto);

        var sut = CreateSut();
        await sut.InitialiseAsync("id1");
        sut.Password = "";

        // Act
        await sut.SaveCommand.ExecuteAsync(null);

        // Assert
        await _credentialStore.DidNotReceive().SavePasswordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _credentialStore.DidNotReceive().DeletePasswordAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ─── TestConnectionCommand — CanExecute ───────────────────────────────────

    [Fact]
    public void TestConnectionCommand_WhenUrlIsEmpty_CannotExecute()
    {
        // Arrange
        var sut = CreateSut();
        sut.Url = "";

        // Assert
        sut.TestConnectionCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void TestConnectionCommand_WhenUrlIsNonEmpty_CanExecute()
    {
        // Arrange
        var sut = CreateSut();
        sut.Url = "http://192.168.1.10:4096";

        // Assert
        sut.TestConnectionCommand.CanExecute(null).Should().BeTrue();
    }

    // ─── TestConnectionCommand — execution ───────────────────────────────────

    [Fact]
    public async Task TestConnectionCommand_WhenServerReachable_SetsConnectionSuccessfulTrue()
    {
        // Arrange
        var sut = CreateSut();
        sut.Url = "http://192.168.1.10:4096";

        // TestConnectionAsync uses "discovery-probe" (no resilience pipeline) to avoid
        // triggering the circuit breaker that protects real API calls on the "opencode" client.
        _httpClientFactory.CreateClient("discovery-probe")
            .Returns(CreateFakeHttpClient(HttpStatusCode.OK, """{"healthy":true,"version":"1.2.3"}"""));

        // Act
        await sut.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        sut.IsConnectionSuccessful.Should().BeTrue();
        sut.IsConnectionTested.Should().BeTrue();
        sut.ConnectionStatusMessage.Should().Contain("Connected");
        sut.ConnectionStatusMessage.Should().Contain("1.2.3");
    }

    [Fact]
    public async Task TestConnectionCommand_WhenServerUnhealthy_SetsConnectionSuccessfulFalse()
    {
        // Arrange
        var sut = CreateSut();
        sut.Url = "http://192.168.1.10:4096";

        _httpClientFactory.CreateClient("discovery-probe")
            .Returns(CreateFakeHttpClient(HttpStatusCode.OK, """{"healthy":false,"version":"1.2.3"}"""));

        // Act
        await sut.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        sut.IsConnectionSuccessful.Should().BeFalse();
        sut.ConnectionStatusMessage.Should().Contain("unhealthy");
    }

    [Fact]
    public async Task TestConnectionCommand_WhenApiReturnsFailure_SetsConnectionSuccessfulFalse()
    {
        // Arrange
        var sut = CreateSut();
        sut.Url = "http://192.168.1.10:4096";

        _httpClientFactory.CreateClient("discovery-probe")
            .Returns(CreateFakeHttpClient(HttpStatusCode.InternalServerError, ""));

        // Act
        await sut.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        sut.IsConnectionSuccessful.Should().BeFalse();
        sut.ConnectionStatusMessage.Should().Contain("HTTP 500");
    }

    [Fact]
    public async Task TestConnectionCommand_WhenApiCallTimesOut_SetsConnectionTimedOutMessage()
    {
        // Arrange
        var sut = CreateSut();
        sut.Url = "http://192.168.1.10:4096";

        _httpClientFactory.CreateClient("discovery-probe")
            .Returns(new HttpClient(new TimeoutHttpMessageHandler()));

        // Act
        await sut.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        sut.IsConnectionSuccessful.Should().BeFalse();
        sut.ConnectionStatusMessage.Should().Contain("timed out");
    }

    [Fact]
    public async Task TestConnectionCommand_WhenInvalidUrl_SetsConnectionFailedWithoutCallingHttpClientFactory()
    {
        // Arrange
        var sut = CreateSut();
        sut.Url = "not-a-url";

        // Act
        await sut.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        sut.IsConnectionSuccessful.Should().BeFalse();
        _httpClientFactory.DidNotReceive().CreateClient(Arg.Any<string>());
    }

    [Fact]
    public async Task TestConnectionCommand_WhenHttpRequestFails_SetsConnectionFailedMessage()
    {
        // Arrange
        var sut = CreateSut();
        sut.Url = "http://192.168.1.10:4096";

        _httpClientFactory.CreateClient("discovery-probe")
            .Returns(new HttpClient(new NetworkErrorHttpMessageHandler()));

        // Act
        await sut.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        sut.IsConnectionSuccessful.Should().BeFalse();
        sut.ConnectionStatusMessage.Should().Contain("Connection failed");
    }

    // ─── SetActiveCommand — CanExecute ────────────────────────────────────────

    [Fact]
    public void SetActiveCommand_WhenIsSavedIsFalse_CannotExecute()
    {
        // Arrange
        var sut = CreateSut();
        // IsSaved defaults to false

        // Assert
        sut.SetActiveCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task SetActiveCommand_WhenIsSavedIsTrue_CanExecute()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto("id1");
        _repository.GetByIdAsync("id1", Arg.Any<CancellationToken>())
            .Returns(dto);

        var sut = CreateSut();
        await sut.InitialiseAsync("id1"); // sets IsSaved = true

        // Assert
        sut.SetActiveCommand.CanExecute(null).Should().BeTrue();
    }

    // ─── SetActiveCommand — execution ────────────────────────────────────────

    [Fact]
    public async Task SetActiveCommand_WhenCalled_CallsSetActiveAsyncThenIsServerReachableAsync()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto("id1");
        _repository.GetByIdAsync("id1", Arg.Any<CancellationToken>())
            .Returns(dto);
        _repository.SetActiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _connectionManager.IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = CreateSut();
        await sut.InitialiseAsync("id1");

        // Act
        await sut.SetActiveCommand.ExecuteAsync(null);

        // Assert
        await _repository.Received(1).SetActiveAsync("id1", Arg.Any<CancellationToken>());
        await _connectionManager.Received(1).IsServerReachableAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetActiveCommand_WhenServerReachable_SetsActivationStatusMessageWithReachable()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto("id1");
        _repository.GetByIdAsync("id1", Arg.Any<CancellationToken>())
            .Returns(dto);
        _repository.SetActiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _connectionManager.IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = CreateSut();
        await sut.InitialiseAsync("id1");

        // Act
        await sut.SetActiveCommand.ExecuteAsync(null);

        // Assert
        sut.ActivationStatusMessage.Should().Contain("reachable");
    }

    [Fact]
    public async Task SetActiveCommand_WhenServerUnreachable_SetsActivationStatusMessageWithUnreachable()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto("id1");
        _repository.GetByIdAsync("id1", Arg.Any<CancellationToken>())
            .Returns(dto);
        _repository.SetActiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _connectionManager.IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(false);

        var sut = CreateSut();
        await sut.InitialiseAsync("id1");

        // Act
        await sut.SetActiveCommand.ExecuteAsync(null);

        // Assert
        sut.ActivationStatusMessage.Should().Contain("unreachable");
    }

    [Fact]
    public async Task SetActiveCommand_WhenCalled_NavigatesBack()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto("id1");
        _repository.GetByIdAsync("id1", Arg.Any<CancellationToken>())
            .Returns(dto);
        _repository.SetActiveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _connectionManager.IsServerReachableAsync(Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = CreateSut();
        await sut.InitialiseAsync("id1");

        // Act
        await sut.SetActiveCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.Received(1).PopAsync(Arg.Any<CancellationToken>());
    }

    // ─── DeleteCommand — CanExecute ───────────────────────────────────────────

    [Fact]
    public void DeleteCommand_WhenInAddMode_CannotExecute()
    {
        // Arrange
        var sut = CreateSut();
        // IsEditMode defaults to false (Add mode)

        // Assert
        sut.DeleteCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteCommand_WhenInEditMode_CanExecute()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto("id1");
        _repository.GetByIdAsync("id1", Arg.Any<CancellationToken>())
            .Returns(dto);

        var sut = CreateSut();
        await sut.InitialiseAsync("id1"); // sets IsEditMode = true

        // Assert
        sut.DeleteCommand.CanExecute(null).Should().BeTrue();
    }

    // ─── DeleteCommand — execution ────────────────────────────────────────────

    [Fact]
    public async Task DeleteCommand_WhenUserCancelsConfirmation_DoesNotCallDeleteAsync()
    {
        // Arrange
        _popupService.ShowConfirmDeleteAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(false);

        var sut = CreateSut();

        // Act
        await sut.DeleteCommand.ExecuteAsync(null);

        // Assert
        await _repository.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteCommand_WhenUserConfirms_CallsDeleteAsyncAndNavigatesBack()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto("id1");
        _repository.GetByIdAsync("id1", Arg.Any<CancellationToken>())
            .Returns(dto);
        _repository.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        _popupService.ShowConfirmDeleteAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = CreateSut();
        await sut.InitialiseAsync("id1");

        // Act
        await sut.DeleteCommand.ExecuteAsync(null);

        // Assert
        await _repository.Received(1).DeleteAsync("id1", Arg.Any<CancellationToken>());
        await _navigationService.Received(1).PopAsync(Arg.Any<CancellationToken>());
    }
}
