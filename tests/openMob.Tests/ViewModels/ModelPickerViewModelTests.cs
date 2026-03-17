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
                "limit": { "context": 200000, "output": 64000 }
            },
            "claude-3-sonnet": {
                "name": "Claude 3 Sonnet",
                "limit": { "context": 200000, "output": 8192 }
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
        _sut.Models.Should().BeEmpty();
        _sut.IsLoading.Should().BeFalse();
        _sut.IsEmpty.Should().BeFalse();
        _sut.SelectedModelId.Should().BeNull();
    }

    // ─── LoadModelsCommand ────────────────────────────────────────────────────

    [Fact]
    public async Task LoadModelsCommand_WhenProvidersExist_SetsIsEmptyFalse()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProviderWithModels() };
        _providerService.GetConfiguredProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);

        // Act
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Assert
        _sut.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task LoadModelsCommand_WhenNoProviders_SetsIsEmptyTrue()
    {
        // Arrange
        _providerService.GetConfiguredProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ProviderDto>());

        // Act
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Assert
        _sut.IsEmpty.Should().BeTrue();
        _sut.Models.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadModelsCommand_ExtractsModelsFromProviderJson()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProviderWithModels() };
        _providerService.GetConfiguredProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);

        // Act
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Assert
        _sut.Models.Should().HaveCount(2);
        _sut.Models.Should().OnlyContain(m => m.ProviderName == "Anthropic");
    }

    [Fact]
    public async Task LoadModelsCommand_SetsModelIdWithProviderPrefix()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProviderWithModels() };
        _providerService.GetConfiguredProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);

        // Act
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Assert
        _sut.Models.Should().Contain(m => m.Id == "anthropic/claude-3-opus");
    }

    [Fact]
    public async Task LoadModelsCommand_ExtractsModelNameFromJson()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProviderWithModels() };
        _providerService.GetConfiguredProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);

        // Act
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Assert
        _sut.Models.Should().Contain(m => m.Name == "Claude 3 Opus");
    }

    [Fact]
    public async Task LoadModelsCommand_ExtractsContextSizeFromJson()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProviderWithModels() };
        _providerService.GetConfiguredProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);

        // Act
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Assert
        _sut.Models.Should().Contain(m => m.ContextSize == "200k tokens");
    }

    [Fact]
    public async Task LoadModelsCommand_WhenModelsJsonIsEmptyObject_SetsIsEmptyTrue()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProvider() };
        _providerService.GetConfiguredProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);

        // Act
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Assert
        _sut.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task LoadModelsCommand_SetsIsLoadingFalseAfterCompletion()
    {
        // Arrange
        _providerService.GetConfiguredProvidersAsync(Arg.Any<CancellationToken>())
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
        _providerService.GetConfiguredProvidersAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Error"));

        // Act
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Assert
        _sut.Models.Should().BeEmpty();
        _sut.IsEmpty.Should().BeTrue();
        _sut.IsLoading.Should().BeFalse();
    }

    // ─── SelectModelCommand ───────────────────────────────────────────────────

    [Fact]
    public async Task SelectModelCommand_SetsSelectedModelId()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProviderWithModels() };
        _providerService.GetConfiguredProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Act
        await _sut.SelectModelCommand.ExecuteAsync("anthropic/claude-3-opus");

        // Assert
        _sut.SelectedModelId.Should().Be("anthropic/claude-3-opus");
    }

    [Fact]
    public async Task SelectModelCommand_UpdatesIsSelectedInModels()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProviderWithModels() };
        _providerService.GetConfiguredProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Act
        await _sut.SelectModelCommand.ExecuteAsync("anthropic/claude-3-opus");

        // Assert
        _sut.Models.Should().ContainSingle(m => m.Id == "anthropic/claude-3-opus" && m.IsSelected);
        _sut.Models.Should().ContainSingle(m => m.Id == "anthropic/claude-3-sonnet" && !m.IsSelected);
    }

    [Fact]
    public async Task SelectModelCommand_ClosesPopup()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProviderWithModels() };
        _providerService.GetConfiguredProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);
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
    public async Task LoadModelsCommand_WithMultipleProviders_ContainsAllModels()
    {
        // Arrange
        var providers = new List<ProviderDto>
        {
            BuildProviderWithModels("anthropic", "Anthropic"),
            BuildProvider("openai", "OpenAI", """{"gpt-4o": {"name": "GPT-4o"}}"""),
        };
        _providerService.GetConfiguredProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);

        // Act
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        // Assert
        _sut.Models.Should().HaveCount(3);
        _sut.IsEmpty.Should().BeFalse();
    }

    // ─── SelectModelCommand — callback mechanism ──────────────────────────────

    [Fact]
    public async Task SelectModelCommand_WhenOnModelSelectedIsSet_InvokesCallbackWithCorrectModelId()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProviderWithModels() };
        _providerService.GetConfiguredProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        string? receivedModelId = null;
        _sut.OnModelSelected = modelId => receivedModelId = modelId;

        // Act
        await _sut.SelectModelCommand.ExecuteAsync("anthropic/claude-3-opus");

        // Assert
        receivedModelId.Should().Be("anthropic/claude-3-opus");
    }

    [Fact]
    public async Task SelectModelCommand_WhenOnModelSelectedIsNull_DoesNotThrow()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProviderWithModels() };
        _providerService.GetConfiguredProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        _sut.OnModelSelected = null;

        // Act
        var act = async () => await _sut.SelectModelCommand.ExecuteAsync("anthropic/claude-3-opus");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SelectModelCommand_InvokesCallbackBeforePopPopupAsync()
    {
        // Arrange
        var providers = new List<ProviderDto> { BuildProviderWithModels() };
        _providerService.GetConfiguredProvidersAsync(Arg.Any<CancellationToken>()).Returns(providers);
        await _sut.LoadModelsCommand.ExecuteAsync(null);

        var callOrder = new List<string>();
        _sut.OnModelSelected = _ => callOrder.Add("callback");
        _popupService.PopPopupAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callOrder.Add("popPopup");
                return Task.CompletedTask;
            });

        // Act
        await _sut.SelectModelCommand.ExecuteAsync("anthropic/claude-3-opus");

        // Assert
        callOrder.Should().ContainInOrder("callback", "popPopup");
    }

    [Fact]
    public void OnModelSelected_DefaultsToNull()
    {
        // Assert
        _sut.OnModelSelected.Should().BeNull();
    }
}
