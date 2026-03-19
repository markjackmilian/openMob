using NSubstitute.ExceptionExtensions;
using openMob.Core.Services;
using openMob.Core.ViewModels;
using openMob.Tests.Helpers;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="ServerManagementViewModel"/>.
/// </summary>
public sealed class ServerManagementViewModelTests
{
    private readonly IServerConnectionRepository _repository;
    private readonly IOpencodeDiscoveryService _discoveryService;
    private readonly INavigationService _navigationService;

    public ServerManagementViewModelTests()
    {
        _repository = Substitute.For<IServerConnectionRepository>();
        _discoveryService = Substitute.For<IOpencodeDiscoveryService>();
        _navigationService = Substitute.For<INavigationService>();
    }

    private ServerManagementViewModel CreateSut()
        => new(_repository, _discoveryService, _navigationService);

    // ─── Helper: async enumerable from a list ─────────────────────────────────

    private static async IAsyncEnumerable<DiscoveredServerDto> ToAsyncEnumerable(
        IEnumerable<DiscoveredServerDto> items,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            yield return item;
            await Task.Yield();
        }
    }

    // ─── Constructor — null guards ────────────────────────────────────────────

    [Fact]
    public void Constructor_WhenRepositoryIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ServerManagementViewModel(null!, _discoveryService, _navigationService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("serverConnectionRepository");
    }

    [Fact]
    public void Constructor_WhenDiscoveryServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ServerManagementViewModel(_repository, null!, _navigationService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("discoveryService");
    }

    [Fact]
    public void Constructor_WhenNavigationServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ServerManagementViewModel(_repository, _discoveryService, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("navigationService");
    }

    // ─── LoadCommand — happy path ─────────────────────────────────────────────

    [Fact]
    public async Task LoadCommand_WhenRepositoryReturnsServers_PopulatesServersCollection()
    {
        // Arrange
        var servers = new List<ServerConnectionDto>
        {
            TestDataBuilder.CreateServerConnectionDto("id1"),
            TestDataBuilder.CreateServerConnectionDto("id2"),
        };
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(servers);

        var sut = CreateSut();

        // Act
        await sut.LoadCommand.ExecuteAsync(null);

        // Assert
        sut.Servers.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadCommand_WhenRepositoryReturnsEmpty_ServersCollectionIsEmpty()
    {
        // Arrange
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ServerConnectionDto>());

        var sut = CreateSut();

        // Act
        await sut.LoadCommand.ExecuteAsync(null);

        // Assert
        sut.Servers.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadCommand_WhenCalled_ResetsDiscoveredServersAndScanCompleted()
    {
        // Arrange
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ServerConnectionDto>());
        _discoveryService.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(new[]
            {
                new DiscoveredServerDto("opencode-4096", "192.168.1.50", 4096, DateTimeOffset.UtcNow),
            }));

        var sut = CreateSut();

        // Run a scan first so DiscoveredServers is non-empty and ScanCompleted is true.
        await sut.ScanCommand.ExecuteAsync(null);

        // Act
        await sut.LoadCommand.ExecuteAsync(null);

        // Assert
        sut.DiscoveredServers.Should().BeEmpty();
        sut.ScanCompleted.Should().BeFalse();
    }

    // ─── LoadCommand — error path ─────────────────────────────────────────────

    [Fact]
    public async Task LoadCommand_WhenRepositoryThrows_SetsLoadError()
    {
        // Arrange
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB unavailable"));

        var sut = CreateSut();

        // Act
        await sut.LoadCommand.ExecuteAsync(null);

        // Assert
        sut.LoadError.Should().Be("Could not load servers. Please try again.");
        sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadCommand_WhenCalledAfterError_ClearsLoadError()
    {
        // Arrange — first call throws
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB unavailable"));

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);
        sut.LoadError.Should().NotBeNull(); // precondition

        // Arrange — second call succeeds
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ServerConnectionDto>());

        // Act
        await sut.LoadCommand.ExecuteAsync(null);

        // Assert
        sut.LoadError.Should().BeNull();
    }

    // ─── ScanCommand — happy path ─────────────────────────────────────────────

    [Fact]
    public async Task ScanCommand_WhenDiscoveryReturnsResults_PopulatesDiscoveredServers()
    {
        // Arrange
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ServerConnectionDto>());
        _discoveryService.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(new[]
            {
                new DiscoveredServerDto("opencode-4096", "192.168.1.50", 4096, DateTimeOffset.UtcNow),
            }));

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        // Act
        await sut.ScanCommand.ExecuteAsync(null);

        // Assert
        sut.DiscoveredServers.Should().HaveCount(1);
        sut.HasDiscoveredServers.Should().BeTrue();
        sut.ScanCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task ScanCommand_WhenDiscoveryReturnsNoResults_DiscoveredServersIsEmpty_ScanCompletedIsTrue()
    {
        // Arrange
        _discoveryService.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(Array.Empty<DiscoveredServerDto>()));

        var sut = CreateSut();

        // Act
        await sut.ScanCommand.ExecuteAsync(null);

        // Assert
        sut.DiscoveredServers.Should().BeEmpty();
        sut.HasDiscoveredServers.Should().BeFalse();
        sut.ScanCompleted.Should().BeTrue();
    }

    // ─── ScanCommand — deduplication ─────────────────────────────────────────

    [Fact]
    public async Task ScanCommand_WhenDiscoveredServerMatchesSavedServerByHostAndPort_IsNotAddedToDiscoveredServers()
    {
        // Arrange
        var savedServer = TestDataBuilder.CreateServerConnectionDto(host: "192.168.1.50", port: 4096);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ServerConnectionDto> { savedServer });
        _discoveryService.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(new[]
            {
                new DiscoveredServerDto("opencode-4096", "192.168.1.50", 4096, DateTimeOffset.UtcNow),
            }));

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        // Act
        await sut.ScanCommand.ExecuteAsync(null);

        // Assert
        sut.DiscoveredServers.Should().BeEmpty();
    }

    [Fact]
    public async Task ScanCommand_WhenDiscoveredServerHasDifferentPort_IsAddedToDiscoveredServers()
    {
        // Arrange
        var savedServer = TestDataBuilder.CreateServerConnectionDto(host: "192.168.1.50", port: 4096);
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ServerConnectionDto> { savedServer });
        _discoveryService.ScanAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(new[]
            {
                new DiscoveredServerDto("opencode-4097", "192.168.1.50", 4097, DateTimeOffset.UtcNow),
            }));

        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        // Act
        await sut.ScanCommand.ExecuteAsync(null);

        // Assert
        sut.DiscoveredServers.Should().HaveCount(1);
    }

    // ─── NavigateToAddCommand ─────────────────────────────────────────────────

    [Fact]
    public async Task NavigateToAddCommand_WhenCalled_NavigatesToServerDetailRoute()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.NavigateToAddCommand.ExecuteAsync(null);

        // Assert
        await _navigationService.Received(1).GoToAsync(
            "server-detail",
            Arg.Any<CancellationToken>());
    }

    // ─── NavigateToEditCommand ────────────────────────────────────────────────

    [Fact]
    public async Task NavigateToEditCommand_WhenCalledWithDto_NavigatesToServerDetailWithServerId()
    {
        // Arrange
        var dto = TestDataBuilder.CreateServerConnectionDto("server-123");
        var sut = CreateSut();

        // Act
        await sut.NavigateToEditCommand.ExecuteAsync(dto);

        // Assert
        await _navigationService.Received(1).GoToAsync(
            "server-detail",
            Arg.Is<IDictionary<string, object>>(d =>
                d.ContainsKey("serverId") && (string)d["serverId"] == "server-123"),
            Arg.Any<CancellationToken>());
    }

    // ─── NavigateToDiscoveredCommand ──────────────────────────────────────────

    [Fact]
    public async Task NavigateToDiscoveredCommand_WhenCalledWithDto_NavigatesToServerDetailWithDiscoveredParams()
    {
        // Arrange
        var dto = new DiscoveredServerDto("opencode-4096", "192.168.1.50", 4096, DateTimeOffset.UtcNow);
        var sut = CreateSut();

        // Act
        await sut.NavigateToDiscoveredCommand.ExecuteAsync(dto);

        // Assert
        await _navigationService.Received(1).GoToAsync(
            "server-detail",
            Arg.Is<IDictionary<string, object>>(d =>
                d.ContainsKey("discoveredHost") && (string)d["discoveredHost"] == "192.168.1.50"),
            Arg.Any<CancellationToken>());
    }
}
