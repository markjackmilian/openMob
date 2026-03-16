using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using openMob.Core.Infrastructure.Settings;

namespace openMob.Core.ViewModels;

/// <summary>
/// ViewModel for the Settings page.
/// </summary>
/// <remarks>
/// <para>
/// Exposes the current theme preference as a human-readable label (<see cref="SelectedThemeLabel"/>)
/// and provides a command (<see cref="ApplyThemeCommand"/>) to change and persist the preference.
/// </para>
/// <para>
/// This ViewModel has zero MAUI dependencies — all platform concerns are delegated to
/// <see cref="IThemeService"/>, which is injected at construction time.
/// </para>
/// </remarks>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IThemeService _themeService;

    /// <summary>
    /// Initialises the <see cref="SettingsViewModel"/> with the required theme service.
    /// </summary>
    /// <param name="themeService">Service used to read and persist the theme preference.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="themeService"/> is <see langword="null"/>.</exception>
    public SettingsViewModel(IThemeService themeService)
    {
        ArgumentNullException.ThrowIfNull(themeService);
        _themeService = themeService;

        // Initialise the label from the currently persisted preference.
        _selectedThemeLabel = MapToLabel(_themeService.GetTheme());
    }

    /// <summary>
    /// Gets or sets the human-readable label for the currently active theme preference.
    /// </summary>
    /// <remarks>
    /// Possible values: <c>"Light"</c>, <c>"Dark"</c>, <c>"System"</c>.
    /// Bound to the right-hand label on the Appearance row in <c>SettingsPage</c>.
    /// </remarks>
    [ObservableProperty]
    private string _selectedThemeLabel;

    /// <summary>
    /// Persists and immediately applies the given theme preference, then updates
    /// <see cref="SelectedThemeLabel"/> to reflect the new selection.
    /// </summary>
    /// <param name="preference">The theme preference to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    [RelayCommand]
    private async Task ApplyThemeAsync(AppThemePreference preference, CancellationToken ct)
    {
        await _themeService.SetThemeAsync(preference, ct);
        SelectedThemeLabel = MapToLabel(preference);
    }

    /// <summary>
    /// Maps an <see cref="AppThemePreference"/> value to its display label string.
    /// </summary>
    /// <param name="preference">The preference to map.</param>
    /// <returns>A human-readable label: <c>"Light"</c>, <c>"Dark"</c>, or <c>"System"</c>.</returns>
    private static string MapToLabel(AppThemePreference preference) => preference switch
    {
        AppThemePreference.Light  => "Light",
        AppThemePreference.Dark   => "Dark",
        _                         => "System",
    };
}
