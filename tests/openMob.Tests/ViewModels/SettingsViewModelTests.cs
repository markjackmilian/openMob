using NSubstitute.ExceptionExtensions;
using openMob.Core.Infrastructure.Settings;
using openMob.Core.Localization;
using openMob.Core.Models;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="SettingsViewModel"/>.
/// </summary>
public sealed class SettingsViewModelTests
{
    private readonly IThemeService _themeService;
    private readonly ILanguageService _languageService;
    private readonly IAppPopupService _popupService;
    private readonly INavigationService _navigationService;

    public SettingsViewModelTests()
    {
        _themeService = Substitute.For<IThemeService>();
        _languageService = Substitute.For<ILanguageService>();
        _popupService = Substitute.For<IAppPopupService>();
        _navigationService = Substitute.For<INavigationService>();
    }

    // ─── Constructor / Initialisation ────────────────────────────────────────

    [Theory]
    [InlineData(AppThemePreference.Light)]
    [InlineData(AppThemePreference.Dark)]
    [InlineData(AppThemePreference.System)]
    public void Constructor_WhenThemeServiceReturnsPreference_SetsCorrectSelectedThemeLabel(
        AppThemePreference preference)
    {
        // Arrange
        _themeService.GetTheme().Returns(preference);

        // Act
        _languageService.GetLanguageCode().Returns("en");

        var sut = new SettingsViewModel(_themeService, _languageService, _popupService, _navigationService);

        // Assert
        var expectedLabel = preference switch
        {
            AppThemePreference.Light => AppResources.Get("Light"),
            AppThemePreference.Dark => AppResources.Get("Dark"),
            _ => AppResources.Get("System"),
        };

        sut.SelectedThemeLabel.Should().Be(expectedLabel);
    }

    [Fact]
    public void Constructor_WhenThemeServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new SettingsViewModel(null!, _languageService, _popupService, _navigationService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("themeService");
    }

    [Fact]
    public void Constructor_WhenNavigationServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        _languageService.GetLanguageCode().Returns("en");
        var act = () => new SettingsViewModel(_themeService, _languageService, _popupService, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("navigationService");
    }

    [Theory]
    [InlineData("en")]
    [InlineData("it")]
    public void Constructor_WhenLanguageServiceReturnsPreference_SetsCorrectSelectedLanguageOption(string languageCode)
    {
        // Arrange
        _themeService.GetTheme().Returns(AppThemePreference.System);
        _languageService.GetLanguageCode().Returns(languageCode);

        // Act
        var sut = new SettingsViewModel(_themeService, _languageService, _popupService, _navigationService);

        // Assert
        sut.SelectedLanguageOption.Should().NotBeNull();
        sut.SelectedLanguageOption!.Code.Should().Be(languageCode);
    }

    [Fact]
    public void Constructor_WhenLanguageServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new SettingsViewModel(_themeService, null!, _popupService, _navigationService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("languageService");
    }

    [Fact]
    public void Constructor_WhenPopupServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new SettingsViewModel(_themeService, _languageService, null!, _navigationService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("popupService");
    }

    // ─── ApplyThemeCommand — service delegation ───────────────────────────────

    [Theory]
    [InlineData(AppThemePreference.Light)]
    [InlineData(AppThemePreference.Dark)]
    [InlineData(AppThemePreference.System)]
    public async Task ApplyThemeCommand_WhenCalled_CallsSetThemeAsyncWithCorrectPreference(
        AppThemePreference preference)
    {
        // Arrange
        _themeService.GetTheme().Returns(AppThemePreference.System);
        _languageService.GetLanguageCode().Returns("en");

        var sut = new SettingsViewModel(_themeService, _languageService, _popupService, _navigationService);

        // Act
        await sut.ApplyThemeCommand.ExecuteAsync(preference);

        // Assert
        await _themeService.Received(1).SetThemeAsync(
            Arg.Is(preference),
            Arg.Any<CancellationToken>());
    }

    // ─── ApplyThemeCommand — label update ────────────────────────────────────

    [Theory]
    [InlineData(AppThemePreference.Light,  "Light")]
    [InlineData(AppThemePreference.Dark,   "Dark")]
    [InlineData(AppThemePreference.System, "System")]
    public async Task ApplyThemeCommand_WhenSetThemeAsyncSucceeds_UpdatesSelectedThemeLabelCorrectly(
        AppThemePreference preference, string expectedLabel)
    {
        // Arrange
        _themeService.GetTheme().Returns(AppThemePreference.System);
        _languageService.GetLanguageCode().Returns("en");

        var sut = new SettingsViewModel(_themeService, _languageService, _popupService, _navigationService);

        // Act
        await sut.ApplyThemeCommand.ExecuteAsync(preference);

        // Assert
        sut.SelectedThemeLabel.Should().Be(expectedLabel);
    }

    // ─── ApplyThemeCommand — error path ──────────────────────────────────────

    [Fact]
    public async Task ApplyThemeCommand_WhenSetThemeAsyncThrows_DoesNotUpdateLabel()
    {
        // Arrange
        _themeService.GetTheme().Returns(AppThemePreference.System);
        _themeService
            .SetThemeAsync(Arg.Any<AppThemePreference>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Theme service failed."));

        _languageService.GetLanguageCode().Returns("en");

        var sut = new SettingsViewModel(_themeService, _languageService, _popupService, _navigationService);
        var labelBeforeCommand = sut.SelectedThemeLabel;

        // Act
        // IAsyncRelayCommand.ExecuteAsync re-throws exceptions from the underlying task.
        // Absorb the exception so we can assert the label state afterwards.
        try
        {
            await sut.ApplyThemeCommand.ExecuteAsync(AppThemePreference.Dark);
        }
        catch (InvalidOperationException)
        {
            // Expected — the service threw; we only care that the label was not updated.
        }

        // Assert
        sut.SelectedThemeLabel.Should().Be(labelBeforeCommand);
    }

    // ─── NavigateToServerManagementCommand ───────────────────────────────────

    [Fact]
    public async Task NavigateToServerManagementCommand_WhenCalled_CallsGoToAsyncWithServerManagementRoute()
    {
        // Arrange
        _themeService.GetTheme().Returns(AppThemePreference.System);
        _languageService.GetLanguageCode().Returns("en");

        var sut = new SettingsViewModel(_themeService, _languageService, _popupService, _navigationService);

        // Act
        await sut.NavigateToServerManagementCommand.ExecuteAsync(null);

        // Assert
        // "///server-management" is required because server-management is a ShellContent element.
        // MAUI does not allow plain relative routing to Shell elements — the triple-slash prefix
        // performs a push navigation that preserves back navigation.
        await _navigationService.Received(1).GoToAsync(
            "///server-management",
            Arg.Any<CancellationToken>());
    }

    // ─── ApplyLanguageCommand — persistence and notification ────────────────

    [Fact]
    public async Task ApplyLanguageCommand_WhenLanguageChanges_SavesPreferenceAndShowsToast()
    {
        // Arrange
        _themeService.GetTheme().Returns(AppThemePreference.System);
        _languageService.GetLanguageCode().Returns("en");

        var sut = new SettingsViewModel(_themeService, _languageService, _popupService, _navigationService);

        // Act
        await sut.ApplyLanguageCommand.ExecuteAsync(new LanguageOption("it", "Italiano"));

        // Assert
        await _languageService.Received(1).SetLanguageCodeAsync("it", Arg.Any<CancellationToken>());
        await _popupService.Received(1).ShowToastAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
