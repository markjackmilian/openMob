using NSubstitute.ExceptionExtensions;
using openMob.Core.Infrastructure.Settings;
using openMob.Core.Services;
using openMob.Core.ViewModels;

namespace openMob.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="SettingsViewModel"/>.
/// </summary>
public sealed class SettingsViewModelTests
{
    private readonly IThemeService _themeService;
    private readonly INavigationService _navigationService;

    public SettingsViewModelTests()
    {
        _themeService = Substitute.For<IThemeService>();
        _navigationService = Substitute.For<INavigationService>();
    }

    // ─── Constructor / Initialisation ────────────────────────────────────────

    [Theory]
    [InlineData(AppThemePreference.Light,  "Light")]
    [InlineData(AppThemePreference.Dark,   "Dark")]
    [InlineData(AppThemePreference.System, "System")]
    public void Constructor_WhenThemeServiceReturnsPreference_SetsCorrectSelectedThemeLabel(
        AppThemePreference preference, string expectedLabel)
    {
        // Arrange
        _themeService.GetTheme().Returns(preference);

        // Act
        var sut = new SettingsViewModel(_themeService, _navigationService);

        // Assert
        sut.SelectedThemeLabel.Should().Be(expectedLabel);
    }

    [Fact]
    public void Constructor_WhenThemeServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new SettingsViewModel(null!, _navigationService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("themeService");
    }

    [Fact]
    public void Constructor_WhenNavigationServiceIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new SettingsViewModel(_themeService, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("navigationService");
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
        var sut = new SettingsViewModel(_themeService, _navigationService);

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
        var sut = new SettingsViewModel(_themeService, _navigationService);

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

        var sut = new SettingsViewModel(_themeService, _navigationService);
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
        var sut = new SettingsViewModel(_themeService, _navigationService);

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
}
