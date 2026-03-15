using System.Text.Json;
using NSubstitute.ExceptionExtensions;
using openMob.Core.Infrastructure.Http.Dtos.Opencode;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="ModelPickerViewModel"/>.
/// </summary>
public sealed class ModelPickerViewModelTests
{
    private readonly IProviderService _providerService;
    private readonly INavigationService _navigationService;
    private readonly IAppPopupService _popupService;
    private readonly ModelPickerViewModel _sut;

    public ModelPickerViewModelTests()
    {
        _providerService = Substitute.For<IProviderService>();
        _navigationService = Substitute.For<INavigationService>();
        _popupService = Substitute.For<IAppPopupService>();

        _sut = new ModelPickerViewModel(_providerService, _navigationService, _popupService);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static ProviderDto BuildProvider(
        string id = "anthropic",
        string name = "Anthropic",
        string? modelsJson = null)
    {
        var models = JsonDocument.Parse(modelsJson ?? "{}").RootElement;
        return new ProviderDto(
            Id: id, Name: name, Source: "config",
            Env: new List<string>(), Key: "sk-test",
            Options: default, Models: models);
    }

    private static ProviderDto BuildProviderWithModels(
        string id = "anthropic",
        string name = "Anthropic")
    {
        var modelsJson = """
        {
            "claude-3-opus": {
                "name": "Claude 3 Opus",
                "context_length": 200000
            },
            "claude-3-sonnet": {
                "name": "Claude 3 Sonnet",
                "context_length": 200000
            }
        }
        """;
        return BuildProvider(id, name, modelsJson);
    }

    // ─── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Assert
        _sut.ProviderGroups.Should().BeEmpty();
        _sut.IsLoading.Should().BeFalse();
        _sut.IsEmpty.Should().BeFalse();
        _sut.HasProviders.Should().BeFalse();
        _sut.SelectedModelId.Should().BeNull();
    }

    // ─── LoadModelsCommand ────────────────────────────────────────────────────

    [Fact]
    public async Task LoadModelsCommand_WhenProvidersExist_SetsHasProvidersTrue()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProviderWithModels() };
        _providerService.GetProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);

        // Act
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Assert
        _sut.HasProviders.Should().BeTrue();
    }

    [Fact]
    public async Task LoadModelsCommand_WhenNoProviders_SetsHasProvidersFalse()
    {
        // Arrange
        _providerService.GetProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProviderDto>());

        // Act
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Assert
        _sut.HasProviders.Should().BeFalse();
        _sut.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task LoadModelsCommand_ExtractsModelsFromProviderJson()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProviderWithModels() };
        _providerService.GetProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);

        // Act
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Assert
        _sut.ProviderGroups.Should().ContainSingle();
        _sut.ProviderGroups[0].ProviderName.Should().Be("Anthropic");
        _sut.ProviderGroups[0].Models.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadModelsCommand_SetsModelIdWithProviderPrefix()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProviderWithModels() };
        _providerService.GetProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);

        // Act
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Assert
        _sut.ProviderGroups[0].Models.Should().Contain(m => m.Id == "anthropic/claude-3-opus");
    }

    [Fact]
    public async Task LoadModelsCommand_ExtractsModelNameFromJson()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProviderWithModels() };
        _providerService.GetProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);

        // Act
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Assert
        _sut.ProviderGroups[0].Models.Should().Contain(m => m.Name == "Claude 3 Opus");
    }

    [Fact]
    public async Task LoadModelsCommand_ExtractsContextSizeFromJson()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProviderWithModels() };
        _providerService.GetProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);

        // Act
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Assert
        _sut.ProviderGroups[0].Models.Should().Contain(m => m.ContextSize == "200k tokens");
    }

    [Fact]
    public async Task LoadModelsCommand_WhenModelsJsonIsEmptyObject_SetsIsEmptyTrue()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProvider() };
        _providerService.GetProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);

        // Act
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Assert
        _sut.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task LoadModelsCommand_SetsIsLoadingFalseAfterCompletion()
    {
        // Arrange
        _providerService.GetProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProviderDto>());

        // Act
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Assert
        _sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadModelsCommand_WhenServiceThrows_SetsIsEmptyTrue()
    {
        // Arrange
        _providerService.GetProvidersAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Error"));

        // Act
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Assert
        _sut.ProviderGroups.Should().BeEmpty();
        _sut.IsEmpty.Should().BeTrue();
        _sut.IsLoading.Should().BeFalse();
    }

    // ─── SelectModelCommand ───────────────────────────────────────────────────

    [Fact]
    public async Task SelectModelCommand_SetsSelectedModelId()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProviderWithModels() };
        _providerService.GetProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Act
        await _sut.SelectModelCommand.ExecuteAsync("anthropic/claude-3-opus");

        // Assert
        _sut.SelectedModelId.Should().Be("anthropic/claude-3-opus");
    }

    [Fact]
    public async Task SelectModelCommand_UpdatesIsSelectedInGroups()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProviderWithModels() };
        _providerService.GetProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Act
        await _sut.SelectModelCommand.ExecuteAsync("anthropic/claude-3-opus");

        // Assert
        _sut.ProviderGroups[0].Models.Should().ContainSingle(m => m.Id == "anthropic/claude-3-opus" && m.IsSelected);
        _sut.ProviderGroups[0].Models.Should().ContainSingle(m => m.Id == "anthropic/claude-3-sonnet" && !m.IsSelected);
    }

    [Fact]
    public async Task SelectModelCommand_ClosesPopup()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProviderWithModels() };
        _providerService.GetProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Act
        await _sut.SelectModelCommand.ExecuteAsync("anthropic/claude-3-opus");

        // Assert
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
    }

    // ─── ConfigureProvidersCommand ────────────────────────────────────────────

    [Fact]
    public async Task ConfigureProvidersCommand_ClosesPopupAndNavigatesToSettings()
    {
        // Act
        await _sut.ConfigureProvidersCommand.ExecuteAsync(null);

        // Assert
        await _popupService.Received(1).PopPopupAsync(Arg.Any<CancellationToken>());
        await _navigationService.Received(1).GoToAsync("settings", Arg.Any<CancellationToken>());
    }

    // ─── LoadModelsCommand — Multiple providers ──────────────────────────────

    [Fact]
    public async Task LoadModelsCommand_WithMultipleProviders_CreatesMultipleGroups()
    {
        // Arrange
        var providers = new List<ProviderDto>
        {
            BuildProviderWithModels("anthropic", "Anthropic"),
            BuildProvider("openai", "OpenAI", """{"gpt-4o": {"name": "GPT-4o"}}"""),
        };
        _providerService.GetProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);

        // Act
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Assert
        _sut.ProviderGroups.Should().HaveCount(2);
        _sut.IsEmpty.Should().BeFalse();
    }
}
