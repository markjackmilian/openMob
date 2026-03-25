using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Infrastructure.Localization;
using openMob.Core.Infrastructure.Logging;
using openMob.Core.Infrastructure.Settings;
using openMob.Core.Localization;
using openMob.Core.Models;
using openMob.Core.Services;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the Settings page.
/// </summary>
/// <remarks>
/// Exposes theme and language preferences, and coordinates persistence with the injected services.
/// </remarks>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IThemeService _themeService;
    private readonly ILanguageService _languageService;
    private readonly IAppPopupService _popupService;
    private readonly INavigationService _navigationService;
    private string _currentLanguageCode;

    /// <summary>
    /// Gets the available language choices.
    /// </summary>
    public IReadOnlyList<LanguageOption> LanguageOptions { get; } =
    [
        new LanguageOption("en", "English"),
        new LanguageOption("it", "Italiano"),
    ];

    /// <summary>
    /// Initialises the <see cref="SettingsViewModel"/> with the required services.
    /// </summary>
    /// <param name="themeService">Service used to read and persist the theme preference.</param>
    /// <param name="languageService">Service used to read and persist the language preference.</param>
    /// <param name="popupService">Service used to show informational messages.</param>
    /// <param name="navigationService">Service for Shell navigation.</param>
    public SettingsViewModel(
        IThemeService themeService,
        ILanguageService languageService,
        IAppPopupService popupService,
        INavigationService navigationService)
    {
        ArgumentNullException.ThrowIfNull(themeService);
        ArgumentNullException.ThrowIfNull(languageService);
        ArgumentNullException.ThrowIfNull(popupService);
        ArgumentNullException.ThrowIfNull(navigationService);

        _themeService = themeService;
        _languageService = languageService;
        _popupService = popupService;
        _navigationService = navigationService;

        _selectedThemeLabel = MapToLabel(_themeService.GetTheme());

        _currentLanguageCode = NormalizeLanguageCode(_languageService.GetLanguageCode());
        _selectedLanguageOption = GetLanguageOption(_currentLanguageCode);
    }

    /// <summary>
    /// Gets or sets the human-readable label for the currently active theme preference.
    /// </summary>
    [ObservableProperty]
    private string _selectedThemeLabel;

    /// <summary>
    /// Gets or sets the selected language option.
    /// </summary>
    [ObservableProperty]
    private LanguageOption? _selectedLanguageOption;

    /// <summary>
    /// Persists and immediately applies the given theme preference, then updates
    /// <see cref="SelectedThemeLabel"/> to reflect the new selection.
    /// </summary>
    [RelayCommand]
    private async Task ApplyThemeAsync(AppThemePreference preference, CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(ApplyThemeAsync), "start");
        try
        {
#endif
        await _themeService.SetThemeAsync(preference, ct);
        SelectedThemeLabel = MapToLabel(preference);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(ApplyThemeAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(ApplyThemeAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Persists the selected language and shows a restart-required message.
    /// </summary>
    [RelayCommand]
    private async Task ApplyLanguageAsync(LanguageOption option, CancellationToken ct)
    {
        if (string.Equals(option.Code, _currentLanguageCode, StringComparison.OrdinalIgnoreCase))
            return;

#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(ApplyLanguageAsync), "start");
        try
        {
#endif
        var normalizedCode = NormalizeLanguageCode(option.Code);
        await _languageService.SetLanguageCodeAsync(normalizedCode, ct);
        _currentLanguageCode = normalizedCode;
        LocalizationHelper.ApplyCulture(normalizedCode);
        await _popupService.ShowToastAsync(AppResources.Get("RestartRequiredMessage"), ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(ApplyLanguageAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(ApplyLanguageAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Navigates to the Server Management page.
    /// </summary>
    [RelayCommand]
    private async Task NavigateToServerManagementAsync(CancellationToken ct)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
        DebugLogger.LogCommand(nameof(NavigateToServerManagementAsync), "start");
        try
        {
#endif
        await _navigationService.GoToAsync("///server-management", ct);
#if DEBUG
        sw.Stop();
        DebugLogger.LogCommand(nameof(NavigateToServerManagementAsync), "complete", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            DebugLogger.LogCommand(nameof(NavigateToServerManagementAsync), "failed", error: $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
#endif
    }

    /// <summary>
    /// Updates the selected language when the picker changes.
    /// </summary>
    /// <param name="value">The selected language option.</param>
    partial void OnSelectedLanguageOptionChanged(LanguageOption? value)
    {
        if (value is null)
            return;

        _ = ApplyLanguageCommand.ExecuteAsync(value);
    }

    /// <summary>
    /// Maps an <see cref="AppThemePreference"/> value to its display label string.
    /// </summary>
    private static string MapToLabel(AppThemePreference preference) => preference switch
    {
        AppThemePreference.Light  => AppResources.Get("Light"),
        AppThemePreference.Dark   => AppResources.Get("Dark"),
        _                         => AppResources.Get("System"),
    };

    /// <summary>
    /// Normalises a language code to the supported set.
    /// </summary>
    private static string NormalizeLanguageCode(string? languageCode)
        => string.Equals(languageCode, "it", StringComparison.OrdinalIgnoreCase) ? "it" : "en";

    /// <summary>
    /// Gets the matching language option for the specified code.
    /// </summary>
    private LanguageOption GetLanguageOption(string languageCode)
        => LanguageOptions.First(option => string.Equals(option.Code, languageCode, StringComparison.OrdinalIgnoreCase));
}
